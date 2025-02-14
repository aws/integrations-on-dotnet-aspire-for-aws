#nullable enable

using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;

namespace MyLambdaAdapter
{
    public static class LambdaHandlerAdapter
    {
        public static LambdaBootstrapBuilder CreateLambdaBootstrap(object functionInstance, string methodName, ILambdaSerializer serializer)
        {
            if (functionInstance is null)
                throw new ArgumentNullException(nameof(functionInstance));
            if (string.IsNullOrEmpty(methodName))
                throw new ArgumentNullException(nameof(methodName));
            if (serializer is null)
                throw new ArgumentNullException(nameof(serializer));

            MethodInfo? methodInfo = functionInstance.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
            if (methodInfo is null)
                throw new InvalidOperationException($"Method '{methodName}' not found on type '{functionInstance.GetType().FullName}'.");

            ParameterInfo[] parameters = methodInfo.GetParameters();
            bool isAsync = typeof(Task).IsAssignableFrom(methodInfo.ReturnType);

            if (parameters.Length == 0)
            {
                // Parameterless handler: () => TOutput or () => Task<TOutput>
                Type tOutput = GetUnderlyingReturnType(methodInfo.ReturnType);
                if (isAsync)
                {
                    // Create delegate using helper for async parameterless methods.
                    MethodInfo? helper = typeof(LambdaHandlerAdapter)
                        .GetMethod(nameof(CreateParameterlessAsyncWrapper), BindingFlags.NonPublic | BindingFlags.Static)
                        ?? throw new InvalidOperationException("Helper for parameterless async wrapper not found.");
                    MethodInfo genericHelper = helper.MakeGenericMethod(tOutput);
                    object? asyncWrapper = genericHelper.Invoke(null, new object[] { functionInstance, methodInfo });
                    if (asyncWrapper is null)
                        throw new InvalidOperationException("Failed to create parameterless async wrapper delegate.");

                    // Expected delegate: Func<Task<TOutput>>
                    Type expectedDelegate = typeof(Func<>).MakeGenericType(typeof(Task<>).MakeGenericType(tOutput));

                    MethodInfo? createMethod = typeof(LambdaBootstrapBuilder)
                        .GetMethods(BindingFlags.Public | BindingFlags.Static)
                        .Where(m => m.Name == "Create" &&
                                    m.IsGenericMethod &&
                                    m.GetGenericArguments().Length == 1)
                        .FirstOrDefault(m =>
                        {
                            ParameterInfo[] ps = m.GetParameters();
                            return ps.Length == 2 && ps[0].ParameterType.Equals(expectedDelegate);
                        });
                    if (createMethod is null)
                        throw new InvalidOperationException("Create overload for parameterless async handler not found.");
                    MethodInfo genericCreate = createMethod.MakeGenericMethod(tOutput);
                    object? result = genericCreate.Invoke(null, new object[] { asyncWrapper, serializer });
                    return result as LambdaBootstrapBuilder
                           ?? throw new InvalidOperationException("Failed to create LambdaBootstrapBuilder for parameterless async handler.");
                }
                else
                {
                    // Synchronous parameterless handler.
                    MethodInfo? helper = typeof(LambdaHandlerAdapter)
                        .GetMethod(nameof(CreateParameterlessSyncWrapper), BindingFlags.NonPublic | BindingFlags.Static)
                        ?? throw new InvalidOperationException("Helper for parameterless sync wrapper not found.");
                    MethodInfo genericHelper = helper.MakeGenericMethod(tOutput);
                    object? syncWrapper = genericHelper.Invoke(null, new object[] { functionInstance, methodInfo });
                    if (syncWrapper is null)
                        throw new InvalidOperationException("Failed to create parameterless sync wrapper delegate.");

                    // Expected delegate: Func<TOutput>
                    Type expectedDelegate = typeof(Func<>).MakeGenericType(tOutput);

                    MethodInfo? createMethod = typeof(LambdaBootstrapBuilder)
                        .GetMethods(BindingFlags.Public | BindingFlags.Static)
                        .Where(m => m.Name == "Create" &&
                                    m.IsGenericMethod &&
                                    m.GetGenericArguments().Length == 1)
                        .FirstOrDefault(m =>
                        {
                            ParameterInfo[] ps = m.GetParameters();
                            return ps.Length == 2 && ps[0].ParameterType.Equals(expectedDelegate);
                        });
                    if (createMethod is null)
                        throw new InvalidOperationException("Create overload for parameterless sync handler not found.");
                    MethodInfo genericCreate = createMethod.MakeGenericMethod(tOutput);
                    object? result = genericCreate.Invoke(null, new object[] { syncWrapper, serializer });
                    return result as LambdaBootstrapBuilder
                           ?? throw new InvalidOperationException("Failed to create LambdaBootstrapBuilder for parameterless sync handler.");
                }
            }
            else if (parameters.Length == 1)
            {
                Type paramType = parameters[0].ParameterType;
                Type tOutput = GetUnderlyingReturnType(methodInfo.ReturnType);
                if (paramType == typeof(ILambdaContext))
                {
                    // Signature: (ILambdaContext) => TOutput or Task<TOutput>
                    if (isAsync)
                    {
                        MethodInfo? helper = typeof(LambdaHandlerAdapter)
                            .GetMethod(nameof(CreateContextOnlyAsyncWrapper), BindingFlags.NonPublic | BindingFlags.Static)
                            ?? throw new InvalidOperationException("Helper for context-only async wrapper not found.");
                        MethodInfo genericHelper = helper.MakeGenericMethod(tOutput);
                        object? asyncWrapper = genericHelper.Invoke(null, new object[] { functionInstance, methodInfo });
                        if (asyncWrapper is null)
                            throw new InvalidOperationException("Failed to create context-only async wrapper delegate.");

                        // Expected delegate: Func<ILambdaContext, Task<TOutput>>
                        Type expectedDelegate = typeof(Func<,>).MakeGenericType(typeof(ILambdaContext), typeof(Task<>).MakeGenericType(tOutput));

                        MethodInfo? createMethod = typeof(LambdaBootstrapBuilder)
                            .GetMethods(BindingFlags.Public | BindingFlags.Static)
                            .Where(m => m.Name == "Create" &&
                                        m.IsGenericMethod &&
                                        m.GetGenericArguments().Length == 1)
                            .FirstOrDefault(m =>
                            {
                                ParameterInfo[] ps = m.GetParameters();
                                return ps.Length == 2 && ps[0].ParameterType.Equals(expectedDelegate);
                            });
                        if (createMethod is null)
                            throw new InvalidOperationException("Create overload for context-only async handler not found.");
                        MethodInfo genericCreate = createMethod.MakeGenericMethod(tOutput);
                        object? result = genericCreate.Invoke(null, new object[] { asyncWrapper, serializer });
                        return result as LambdaBootstrapBuilder
                               ?? throw new InvalidOperationException("Failed to create LambdaBootstrapBuilder for context-only async handler.");
                    }
                    else
                    {
                        MethodInfo? helper = typeof(LambdaHandlerAdapter)
                            .GetMethod(nameof(CreateContextOnlySyncWrapper), BindingFlags.NonPublic | BindingFlags.Static)
                            ?? throw new InvalidOperationException("Helper for context-only sync wrapper not found.");
                        MethodInfo genericHelper = helper.MakeGenericMethod(tOutput);
                        object? syncWrapper = genericHelper.Invoke(null, new object[] { functionInstance, methodInfo });
                        if (syncWrapper is null)
                            throw new InvalidOperationException("Failed to create context-only sync wrapper delegate.");

                        // Expected delegate: Func<ILambdaContext, TOutput>
                        Type expectedDelegate = typeof(Func<,>).MakeGenericType(typeof(ILambdaContext), tOutput);

                        MethodInfo? createMethod = typeof(LambdaBootstrapBuilder)
                            .GetMethods(BindingFlags.Public | BindingFlags.Static)
                            .Where(m => m.Name == "Create" &&
                                        m.IsGenericMethod &&
                                        m.GetGenericArguments().Length == 1)
                            .FirstOrDefault(m =>
                            {
                                ParameterInfo[] ps = m.GetParameters();
                                return ps.Length == 2 && ps[0].ParameterType.Equals(expectedDelegate);
                            });
                        if (createMethod is null)
                            throw new InvalidOperationException("Create overload for context-only sync handler not found.");
                        MethodInfo genericCreate = createMethod.MakeGenericMethod(tOutput);
                        object? result = genericCreate.Invoke(null, new object[] { syncWrapper, serializer });
                        return result as LambdaBootstrapBuilder
                               ?? throw new InvalidOperationException("Failed to create LambdaBootstrapBuilder for context-only sync handler.");
                    }
                }
                else
                {
                    // Signature: (TInput) => TOutput or Task<TOutput>
                    if (isAsync)
                    {
                        MethodInfo? helper = typeof(LambdaHandlerAdapter)
                            .GetMethod(nameof(CreateOneParameterAsyncWrapper), BindingFlags.NonPublic | BindingFlags.Static)
                            ?? throw new InvalidOperationException("Helper for one-parameter async wrapper not found.");
                        MethodInfo genericHelper = helper.MakeGenericMethod(paramType, tOutput);
                        object? asyncWrapper = genericHelper.Invoke(null, new object[] { functionInstance, methodInfo });
                        if (asyncWrapper is null)
                            throw new InvalidOperationException("Failed to create one-parameter async wrapper delegate.");

                        // Expected delegate: Func<TInput, Task<TOutput>>
                        Type expectedDelegate = typeof(Func<,>).MakeGenericType(paramType, typeof(Task<>).MakeGenericType(tOutput));

                        MethodInfo? createMethod = typeof(LambdaBootstrapBuilder)
                            .GetMethods(BindingFlags.Public | BindingFlags.Static)
                            .Where(m => m.Name == "Create" &&
                                        m.IsGenericMethod &&
                                        m.GetGenericArguments().Length == 2)
                            .FirstOrDefault(m =>
                            {
                                ParameterInfo[] ps = m.GetParameters();
                                return ps.Length == 2 && ps[0].ParameterType.IsAssignableFrom(expectedDelegate);
                            });
                        if (createMethod is null)
                            throw new InvalidOperationException("Create overload for one-parameter async handler not found.");
                        MethodInfo genericCreate = createMethod.MakeGenericMethod(paramType, tOutput);
                        object? result = genericCreate.Invoke(null, new object[] { asyncWrapper, serializer });
                        return result as LambdaBootstrapBuilder
                               ?? throw new InvalidOperationException("Failed to create LambdaBootstrapBuilder for one-parameter async handler.");
                    }
                    else
                    {
                        MethodInfo? helper = typeof(LambdaHandlerAdapter)
                            .GetMethod(nameof(CreateOneParameterSyncWrapper), BindingFlags.NonPublic | BindingFlags.Static)
                            ?? throw new InvalidOperationException("Helper for one-parameter sync wrapper not found.");
                        MethodInfo genericHelper = helper.MakeGenericMethod(paramType, tOutput);
                        object? syncWrapper = genericHelper.Invoke(null, new object[] { functionInstance, methodInfo });
                        if (syncWrapper is null)
                            throw new InvalidOperationException("Failed to create one-parameter sync wrapper delegate.");

                        // Expected delegate: Func<TInput, TOutput>
                        Type expectedDelegate = typeof(Func<,>).MakeGenericType(paramType, tOutput);

                        MethodInfo? createMethod = typeof(LambdaBootstrapBuilder)
                            .GetMethods(BindingFlags.Public | BindingFlags.Static)
                            .Where(m => m.Name == "Create" &&
                                        m.IsGenericMethod &&
                                        m.GetGenericArguments().Length == 2)
                            .FirstOrDefault(m =>
                            {
                                ParameterInfo[] ps = m.GetParameters();
                                return ps.Length == 2 && ps[0].ParameterType.Equals(expectedDelegate);
                            });
                        if (createMethod is null)
                            throw new InvalidOperationException("Create overload for one-parameter sync handler not found.");
                        MethodInfo genericCreate = createMethod.MakeGenericMethod(paramType, tOutput);
                        object? result = genericCreate.Invoke(null, new object[] { syncWrapper, serializer });
                        return result as LambdaBootstrapBuilder
                               ?? throw new InvalidOperationException("Failed to create LambdaBootstrapBuilder for one-parameter sync handler.");
                    }
                }
            }
            else if (parameters.Length == 2)
            {
                // Two-parameter handler: (TInput, ILambdaContext) => TOutput or Task<TOutput>
                if (parameters[1].ParameterType != typeof(ILambdaContext))
                    throw new NotSupportedException("For two-parameter handlers, the second parameter must be of type ILambdaContext.");
                Type tInput = parameters[0].ParameterType;
                Type tOutput = GetUnderlyingReturnType(methodInfo.ReturnType);
                if (isAsync)
                {
                    MethodInfo? helper = typeof(LambdaHandlerAdapter)
                        .GetMethod(nameof(CreateTwoParameterAsyncWrapper), BindingFlags.NonPublic | BindingFlags.Static)
                        ?? throw new InvalidOperationException("Helper for two-parameter async wrapper not found.");
                    MethodInfo genericHelper = helper.MakeGenericMethod(tInput, tOutput);
                    object? asyncWrapper = genericHelper.Invoke(null, new object[] { functionInstance, methodInfo });
                    if (asyncWrapper is null)
                        throw new InvalidOperationException("Failed to create two-parameter async wrapper delegate.");

                    // Expected delegate: Func<TInput, ILambdaContext, Task<TOutput>>
                    Type expectedDelegate = typeof(Func<,,>).MakeGenericType(tInput, typeof(ILambdaContext), typeof(Task<>).MakeGenericType(tOutput));

                    MethodInfo? createMethod = typeof(LambdaBootstrapBuilder)
                        .GetMethods(BindingFlags.Public | BindingFlags.Static)
                        .Where(m => m.Name == "Create" &&
                                    m.IsGenericMethod &&
                                    m.GetGenericArguments().Length == 2)
                        .FirstOrDefault(m =>
                        {
                            ParameterInfo[] ps = m.GetParameters();
                            return ps.Length == 2 && ps[0].ParameterType.IsAssignableFrom(expectedDelegate);
                        });
                    if (createMethod is null)
                        throw new InvalidOperationException("Create overload for two-parameter async handler not found.");
                    MethodInfo genericCreate = createMethod.MakeGenericMethod(tInput, tOutput);
                    object? result = genericCreate.Invoke(null, new object[] { asyncWrapper, serializer });
                    return result as LambdaBootstrapBuilder
                           ?? throw new InvalidOperationException("Failed to create LambdaBootstrapBuilder for two-parameter async handler.");
                }
                else
                {
                    MethodInfo? helper = typeof(LambdaHandlerAdapter)
                        .GetMethod(nameof(CreateTwoParameterSyncWrapper), BindingFlags.NonPublic | BindingFlags.Static)
                        ?? throw new InvalidOperationException("Helper for two-parameter sync wrapper not found.");
                    MethodInfo genericHelper = helper.MakeGenericMethod(tInput, tOutput);
                    object? syncWrapper = genericHelper.Invoke(null, new object[] { functionInstance, methodInfo });
                    if (syncWrapper is null)
                        throw new InvalidOperationException("Failed to create two-parameter sync wrapper delegate.");

                    // Expected delegate: Func<TInput, ILambdaContext, TOutput>
                    Type expectedDelegate = typeof(Func<,,>).MakeGenericType(tInput, typeof(ILambdaContext), tOutput);

                    var availableCreateMethods = typeof(LambdaBootstrapBuilder)
                        .GetMethods(BindingFlags.Public | BindingFlags.Static);
                    // For two-parameter sync handlers, use equality for delegate match.
                    MethodInfo? createMethod = availableCreateMethods
                        .Where(m => m.Name == "Create" &&
                                    m.IsGenericMethod &&
                                    m.GetGenericArguments().Length == 2)
                        .FirstOrDefault(m =>
                        {
                            ParameterInfo[] ps = m.GetParameters();
                            return ps.Length == 2 && ps[0].ParameterType == expectedDelegate;
                        });
                    if (createMethod is null)
                        throw new InvalidOperationException("Create overload for two-parameter sync handler not found.");
                    MethodInfo genericCreate = createMethod.MakeGenericMethod(tInput, tOutput);
                    object? result = genericCreate.Invoke(null, new object[] { syncWrapper, serializer });
                    return result as LambdaBootstrapBuilder
                           ?? throw new InvalidOperationException("Failed to create LambdaBootstrapBuilder for two-parameter sync handler.");
                }
            }
            else
            {
                throw new NotSupportedException("Handler methods with more than 2 parameters are not supported.");
            }
        }

        private static Type GetUnderlyingReturnType(Type returnType)
        {
            if (typeof(Task).IsAssignableFrom(returnType) && returnType.IsGenericType)
                return returnType.GetGenericArguments()[0];
            return returnType;
        }

        private static object? GetTaskResult(Task task)
        {
            Type taskType = task.GetType();
            if (taskType.IsGenericType)
            {
                PropertyInfo? resultProperty = taskType.GetProperty("Result");
                return resultProperty?.GetValue(task);
            }
            return null;
        }

        // --- Helper Methods for Typed Wrappers ---

        private static Func<Task<TOutput>> CreateParameterlessAsyncWrapper<TOutput>(object functionInstance, MethodInfo methodInfo)
        {
            return async () =>
            {
                object? result = methodInfo.Invoke(functionInstance, new object?[] { });
                if (result is null)
                    throw new InvalidOperationException("Method invocation returned null.");
                if (result is Task task)
                {
                    await task.ConfigureAwait(false);
                    object? taskResult = GetTaskResult(task);
                    if (taskResult is null)
                        throw new InvalidOperationException("Task result is null.");
                    return (TOutput)taskResult;
                }
                return (TOutput)result;
            };
        }

        private static Func<TOutput> CreateParameterlessSyncWrapper<TOutput>(object functionInstance, MethodInfo methodInfo)
        {
            return () =>
            {
                object? result = methodInfo.Invoke(functionInstance, new object?[] { });
                if (result is null)
                    throw new InvalidOperationException("Method invocation returned null.");
                return (TOutput)result;
            };
        }

        private static Func<ILambdaContext, Task<TOutput>> CreateContextOnlyAsyncWrapper<TOutput>(object functionInstance, MethodInfo methodInfo)
        {
            return async (ILambdaContext context) =>
            {
                object? result = methodInfo.Invoke(functionInstance, new object?[] { (object?)context });
                if (result is null)
                    throw new InvalidOperationException("Method invocation returned null.");
                if (result is Task task)
                {
                    await task.ConfigureAwait(false);
                    object? taskResult = GetTaskResult(task);
                    if (taskResult is null)
                        throw new InvalidOperationException("Task result is null.");
                    return (TOutput)taskResult;
                }
                return (TOutput)result;
            };
        }

        private static Func<ILambdaContext, TOutput> CreateContextOnlySyncWrapper<TOutput>(object functionInstance, MethodInfo methodInfo)
        {
            return (ILambdaContext context) =>
            {
                object? result = methodInfo.Invoke(functionInstance, new object?[] { (object?)context });
                if (result is null)
                    throw new InvalidOperationException("Method invocation returned null.");
                return (TOutput)result;
            };
        }

        private static Func<TInput, Task<TOutput>> CreateOneParameterAsyncWrapper<TInput, TOutput>(object functionInstance, MethodInfo methodInfo)
        {
            return async (TInput input) =>
            {
                object? result = methodInfo.Invoke(functionInstance, new object?[] { (object?)input });
                if (result is null)
                    throw new InvalidOperationException("Method invocation returned null.");
                if (result is Task task)
                {
                    await task.ConfigureAwait(false);
                    object? taskResult = GetTaskResult(task);
                    if (taskResult is null)
                        throw new InvalidOperationException("Task result is null.");
                    return (TOutput)taskResult;
                }
                return (TOutput)result;
            };
        }

        private static Func<TInput, TOutput> CreateOneParameterSyncWrapper<TInput, TOutput>(object functionInstance, MethodInfo methodInfo)
        {
            return (TInput input) =>
            {
                object? result = methodInfo.Invoke(functionInstance, new object?[] { (object?)input });
                if (result is null)
                    throw new InvalidOperationException("Method invocation returned null.");
                return (TOutput)result;
            };
        }

        private static Func<TInput, ILambdaContext, Task<TOutput>> CreateTwoParameterAsyncWrapper<TInput, TOutput>(object functionInstance, MethodInfo methodInfo)
        {
            return async (TInput input, ILambdaContext context) =>
            {
                object? result = methodInfo.Invoke(functionInstance, new object?[] { (object?)input, (object?)context });
                if (result is null)
                    throw new InvalidOperationException("Method invocation returned null.");
                if (result is Task task)
                {
                    await task.ConfigureAwait(false);
                    object? taskResult = GetTaskResult(task);
                    if (taskResult is null)
                        throw new InvalidOperationException("Task result is null.");
                    return (TOutput)taskResult;
                }
                return (TOutput)result;
            };
        }

        private static Func<TInput, ILambdaContext, TOutput> CreateTwoParameterSyncWrapper<TInput, TOutput>(object functionInstance, MethodInfo methodInfo)
        {
            return (TInput input, ILambdaContext context) =>
            {
                object? result = methodInfo.Invoke(functionInstance, new object?[] { (object?)input, (object?)context });
                if (result is null)
                    throw new InvalidOperationException("Method invocation returned null.");
                return (TOutput)result;
            };
        }
    }
}
