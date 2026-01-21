using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aspire.Hosting.AWS.Utils.Internal;

internal interface IAWSEnvironmentService
{
    string[] GetCommandLineArgs();
}

internal class DefaultAWSEnvironmentService : IAWSEnvironmentService
{
    public string[] GetCommandLineArgs()
    {
        return Environment.GetCommandLineArgs();
    }
}