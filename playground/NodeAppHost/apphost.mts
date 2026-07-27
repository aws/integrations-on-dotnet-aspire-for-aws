// Aspire TypeScript/Node.js AppHost exercising the AWS polyglot features:
//  - AWS SDK configuration
//  - DynamoDB Local
//  - A Lambda function using the executable programming model
//  - A Lambda function using the class library programming model
//  - Both Lambda functions fronted by the API Gateway emulator
//
// Run with: aspire run   (pack the local Aspire.Hosting.AWS build first — see README.md)

import { createBuilder, Method, APIGatewayType } from './.aspire/modules/aspire.mjs';

const builder = await createBuilder();

// AWS SDK configuration shared by the resources below.
const sdkConfig = await builder.addAWSSDKConfig()
    .withRegion('us-west-2');

// Local DynamoDB container so Lambda functions can read/write without connecting to real DynamoDB.
const dynamoDBLocal = await builder.addAWSDynamoDBLocal('DynamoDBLocal');

// Lambda function using the executable programming model. The handler is the assembly name.
const toUpper = await builder
    .addAWSLambdaFunction('ToUpperFunction', './ToUpperFunctionExecutable', 'ToUpperFunctionExecutable')
    .withAWSSDKConfigReference(sdkConfig)
    .withDynamoDBLocalReference(dynamoDBLocal);

// Lambda functions using the class library programming model. The handler is "Assembly::Type::Method".
const addFunction = await builder
    .addAWSLambdaFunction('AddFunction', './CalculatorFunctions',
        'CalculatorFunctions::CalculatorFunctions.Functions::Add')
    .withAWSSDKConfigReference(sdkConfig);

const subtractFunction = await builder
    .addAWSLambdaFunction('SubtractFunction', './CalculatorFunctions',
        'CalculatorFunctions::CalculatorFunctions.Functions::Subtract')
    .withAWSSDKConfigReference(sdkConfig);

// API Gateway emulator fronting the Lambda functions. Note the AWS-specific method name
// withAPIGatewayLambdaReference (the polyglot SDK pins AWS reference methods to distinct names so
// they do not collide with the core Aspire withReference capability).
await builder.addAWSAPIGatewayEmulator('APIGatewayEmulator', APIGatewayType.HttpV2)
    .withAPIGatewayLambdaReference(toUpper, Method.Post, '/toupper')
    .withAPIGatewayLambdaReference(addFunction, Method.Get, '/add/{x}/{y}')
    .withAPIGatewayLambdaReference(subtractFunction, Method.Get, '/subtract/{x}/{y}');

await builder.build().run();
