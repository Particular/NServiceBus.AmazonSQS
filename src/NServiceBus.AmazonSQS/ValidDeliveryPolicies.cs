namespace NServiceBus.Transports.SQS
{
    // doesn't help to make this a struct since it gets boxed anyway by the extension bag
    class ValidDeliveryPolicies
    {
        ValidDeliveryPolicies()
        {
        }

        public static readonly ValidDeliveryPolicies Instance = new ValidDeliveryPolicies();
    }
}