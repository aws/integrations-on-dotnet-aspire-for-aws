// See https://aka.ms/new-console-template for more information

using Amazon.Lambda.RuntimeSupport;

RuntimeSupportInitializer runtimeSupportInitializer = new RuntimeSupportInitializer("MultiplyLambdaFunctionLibrary::MultiplyLambdaFunctionLibrary.Function::FunctionHandler");
await runtimeSupportInitializer.RunLambdaBootstrap();

// await LambdaBootstrapBuilder.Create(
//         (Stream inputStream, ILambdaContext context) =>
//         {
//             using var reader = new StreamReader(inputStream);
//             string input = reader.ReadToEnd();
//                 
//             // Create an instance of your function class and call the handler.
//             var function = new Function();
//             return function.FunctionHandler(input, context);
//         },
//         new DefaultLambdaJsonSerializer())
//     .Build()
//     .RunAsync();