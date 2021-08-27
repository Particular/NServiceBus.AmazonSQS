using System.Threading.Tasks;
using Pulumi;

internal class Program
{
    public static Task<int> Main()
    {
        return Deployment.RunAsync<DeleteS3BucketsStack>();
    }
}
