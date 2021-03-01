namespace NServiceBus
{
    using Amazon.S3.Model;

    /// <summary>
    /// S3 encryption settings.
    /// </summary>
    public abstract class S3EncryptionMethod
    {
        /// <summary>
        /// Modifies the get request to retrieve the message body from S3.
        /// </summary>
        protected internal abstract void ModifyGetRequest(GetObjectRequest get);

        /// <summary>
        /// Modifies the put request to upload the message body to S3.
        /// </summary>
        protected internal abstract void ModifyPutRequest(PutObjectRequest put);
    }
}