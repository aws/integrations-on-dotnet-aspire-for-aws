using Amazon.CDK;
using Amazon.CDK.AWS.EC2;
using Aspire.Hosting.AWS.Deployment;
using Constructs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aspire.Hosting.AWS.Integ.Tests.Deployment;

internal class DefaultVpcStack : Stack
{
    public DefaultVpcStack(Construct scope, string id, IStackProps? props = null) : base(scope, id, props)
    {
        DefaultVpc = Vpc.FromLookup(this, "DefaultVpc", new VpcLookupOptions
        {
            IsDefault = true
        });
    }

    [DefaultVpc]
    public IVpc DefaultVpc { get; private set; }
}
