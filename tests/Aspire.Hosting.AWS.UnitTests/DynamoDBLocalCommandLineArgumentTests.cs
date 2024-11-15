﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Aspire.Hosting.AWS.DynamoDB;
using Xunit;

namespace Aspire.Hosting.AWS.Tests;

public class DynamoDBLocalCommandLineArgumentTests
{
    [Fact]
    public void CommandLineArgumentTests()
    {
        CompareArguments(new DynamoDBLocalOptions { }, new string[0]);
        CompareArguments(new DynamoDBLocalOptions {SharedDb = true }, "-sharedDb");
        CompareArguments(new DynamoDBLocalOptions { DisableDynamoDBLocalTelemetry = true }, "-disableTelemetry");
        // The value "/storage" is the path in the container that would be mapped to "C:/temp"
        CompareArguments(new DynamoDBLocalOptions { LocalStorageDirectory = "C:/temp" }, "-dbPath", "/storage");
        CompareArguments(new DynamoDBLocalOptions { DelayTransientStatuses = true }, "-delayTransientStatuses");
    }

    private void CompareArguments(DynamoDBLocalOptions options, params string[] expectedArguments)
    {
        var computedArguments = new DynamoDBLocalResource("resource", options).CreateContainerImageArguments();

        Assert.Equal(computedArguments.Length, expectedArguments.Length + 3);
        Assert.Equal("-Djava.library.path=./DynamoDBLocal_lib", computedArguments[0]);
        Assert.Equal("-jar", computedArguments[1]);
        Assert.Equal("DynamoDBLocal.jar", computedArguments[2]);

        for(var i = 0; i < expectedArguments.Length; i++)
        {
            Assert.Equal(expectedArguments[i], computedArguments[i + 3]);
        }
    }
}
