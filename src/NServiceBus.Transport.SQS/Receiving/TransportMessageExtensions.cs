#nullable enable
namespace NServiceBus.Transport.SQS.Extensions;

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3.Model;
using Amazon.SQS.Model;

static class TransportMessageExtensions
{
    public static void CopyMessageAttributes(this TransportMessage transportMessage, Dictionary<string, MessageAttributeValue>? receiveMessageAttributes)
    {
        foreach (var messageAttribute in receiveMessageAttributes ??
                                         Enumerable.Empty<KeyValuePair<string, MessageAttributeValue>>())
        {
            // The message ID requires complicated special handling and in the majority of cases
            // the NServiceBus message ID when present on the headers might take precedence
            if (messageAttribute.Key == Headers.MessageId)
            {
                continue;
            }

            transportMessage.Headers[messageAttribute.Key] = messageAttribute.Value.StringValue;
        }

        // These headers are not needed in the transport message and would only blow up the message size.
        _ = transportMessage.Headers.Remove(TransportHeaders.Headers);
        _ = transportMessage.Headers.Remove(TransportHeaders.DelaySeconds);
        _ = transportMessage.Headers.Remove(TransportHeaders.TimeToBeReceived);
    }

    public static async ValueTask<(ReadOnlyMemory<byte> MessageBody, byte[]? MessageBodyBuffer)> RetrieveBody(this TransportMessage transportMessage, string messageId, S3Settings s3Settings, ArrayPool<byte> arrayPool,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(transportMessage.S3BodyKey))
        {
            if (transportMessage.Body == TransportMessage.EmptyMessage)
            {
                return EmptyMessage;
            }

            var isNativeMessage = transportMessage.Headers.Keys.Contains(TransportHeaders.Headers);
            return ConvertBody(transportMessage.Body, arrayPool, isNativeMessage);
        }

        if (s3Settings == null)
        {
            throw new Exception($"The message {messageId} contains the ID of the body stored in S3 but this endpoint is not configured to use S3 for body storage.");
        }

        var getObjectRequest = new GetObjectRequest
        {
            BucketName = s3Settings.BucketName,
            Key = transportMessage.S3BodyKey
        };

        s3Settings.NullSafeEncryption.ModifyGetRequest(getObjectRequest);

        var s3GetResponse = await s3Settings.S3Client.GetObjectAsync(getObjectRequest, cancellationToken)
            .ConfigureAwait(false);

        int contentLength = (int)s3GetResponse.ContentLength;
        var buffer = arrayPool.Rent(contentLength);
        using var memoryStream = new MemoryStream(buffer);
        await s3GetResponse.ResponseStream.CopyToAsync(memoryStream, 81920, cancellationToken).ConfigureAwait(false);
        return (buffer.AsMemory(0, contentLength), buffer);
    }

    static (ReadOnlyMemory<byte> MessageBody, byte[]? MessageBodyBuffer) ConvertBody(string body, ArrayPool<byte> arrayPool, bool isNativeMessage)
    {
        var encoding = Encoding.UTF8;

        if (isNativeMessage)
        {
            return GetNonEncodedBody(body, arrayPool, null, encoding);
        }

        var buffer = GetBuffer(body, arrayPool, encoding);
        if (Convert.TryFromBase64String(body, buffer, out var writtenBytes))
        {
            return (buffer.AsMemory(0, writtenBytes), buffer);
        }

        return GetNonEncodedBody(body, arrayPool, buffer, encoding);
    }

    static (ReadOnlyMemory<byte> MessageBody, byte[]? MessageBodyBuffer) GetNonEncodedBody(string body, ArrayPool<byte> arrayPool, byte[]? buffer, Encoding encoding)
    {
        buffer ??= GetBuffer(body, arrayPool, encoding);
        var writtenBytes = encoding.GetBytes(body, 0, body.Length, buffer, 0);
        return (buffer.AsMemory(0, writtenBytes), buffer);
    }

    static byte[] GetBuffer(string body, ArrayPool<byte> arrayPool, Encoding encoding)
    {
        var length = encoding.GetMaxByteCount(body.Length);
        return arrayPool.Rent(length);
    }

    static readonly (ReadOnlyMemory<byte> MessageBody, byte[]? MessageBodyBuffer)
        EmptyMessage = (Array.Empty<byte>(), null);
}