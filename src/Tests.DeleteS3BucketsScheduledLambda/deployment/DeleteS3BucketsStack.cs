using Pulumi;
using Pulumi.Aws.Iam;
using Pulumi.Aws.Lambda;
using Pulumi.Aws.CloudWatch;


internal class DeleteS3BucketsStack : Stack
{
    private const int LambdaMaxAllowedExecutionTimeInSeconds = 60;
    public DeleteS3BucketsStack()
    {
        var lambda = new Function("DeleteSQSTransportTestsExpiredAssets",
            new FunctionArgs
            {
                Runtime = "dotnetcore3.1",
                Code = new FileArchive("../lambda/bin/Debug/netcoreapp3.1/publish"),
                Handler = "DeleteS3Buckets::DeleteS3Buckets.Function::FunctionHandler",
                Role = CreateLambdaRole().Arn,
                Timeout = LambdaMaxAllowedExecutionTimeInSeconds
            });

        Lambda = lambda.Arn;

        CreateCloudWatchEventRule(lambda);
    }

    private static Role CreateLambdaRole()
    {
        var lambdaRole = new Role("lambdaRole", new RoleArgs
        {
            AssumeRolePolicy =
                @"{
                ""Version"": ""2012-10-17"",
                ""Statement"": [
                    {
                        ""Action"": ""sts:AssumeRole"",
                        ""Principal"": {
                            ""Service"": ""lambda.amazonaws.com""
                        },
                        ""Effect"": ""Allow"",
                        ""Sid"": """"
                    }
                ]
            }"
        });

        new RolePolicy("lambdaLogPolicy", new RolePolicyArgs
        {
            Role = lambdaRole.Id,
            Policy =
                @"{
                ""Version"": ""2012-10-17"",
                ""Statement"": [{
                    ""Effect"": ""Allow"",
                    ""Action"": [
                        ""logs:CreateLogGroup"",
                        ""logs:CreateLogStream"",
                        ""logs:PutLogEvents""
                    ],
                    ""Resource"": ""arn:aws:logs:*:*:*""
                }]
            }"
        });

        new RolePolicy("lambdaS3DeleteBucketAccess", new RolePolicyArgs
        {
            Role = lambdaRole.Id,
            Policy =
                @"{
                ""Version"": ""2012-10-17"",
                ""Statement"": [{
                    ""Effect"": ""Allow"",
                    ""Action"": ""s3:*"",
                    ""Resource"": ""*""
                }]
            }"
        });

        return lambdaRole;
    }

    private static void CreateCloudWatchEventRule(Function lambda)
    {
        var console = new EventRule("invoke-sqs-acceptance-tests-cleaner",
            new EventRuleArgs { ScheduleExpression = "rate(2 minutes)" });

        new EventTarget("invokeLambda", new EventTargetArgs { Rule = console.Id, Arn = lambda.Arn, });

        new Permission("allowCloudwatch", new PermissionArgs
        {
            Action = "lambda:InvokeFunction",
            Function = lambda.Name,
            Principal = "events.amazonaws.com",
            SourceArn = console.Arn,
        });
    }

    [Output] public Output<string> Lambda { get; set; }
}