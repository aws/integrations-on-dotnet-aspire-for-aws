## Integrations with .NET Aspire for AWS

This repositry contains the integrations with [.NET Aspire](https://github.com/dotnet/aspire) for AWS. The AWS integrations focus on provisioning and working with AWS application resources in development environment. Making the dev inner loop of iterating over application code with AWS resource seamless without having to leave the development environment.

## Integrations

The following are the list of AWS integrations currently supported for .NET Aspire.

### Aspire.Hosting.AWS

The hosting package to include in Aspire AppHost projects for provisioning and configuring AWS resources for Aspire applications. The package contains the following features. 

* Configure AWS credentials and region for AWS SDK for .NET
* Provisioning AWS resources with CloudFormation template
* Provisioning AWS resources with AWS Cloud Development Kit (CDK)

Check out the package's [README](./src/Aspire.Hosting.AWS/README.md) for a deeper explanation of these features.

[![nuget](https://img.shields.io/nuget/v/Aspire.Hosting.AWS.svg) ![downloads](https://img.shields.io/nuget/dt/Aspire.Hosting.AWS.svg)](https://www.nuget.org/packages/Aspire.Hosting.AWS/)

## Getting Help

For feature requests or issues using this tool please open an [issue in this repository](https://github.com/aws/integrations-on-dotnet-aspire-for-aws/issues).

## Contributing
We welcome community contributions and pull requests. See [CONTRIBUTING](https://github.com/aws/integrations-on-dotnet-aspire-for-aws/blob/main/CONTRIBUTING.md) for information on how to set up a development environment and submit code.

## License

This library is licensed under the MIT-0 License. See the LICENSE file.

