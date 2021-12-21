using System;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Bridge.Commons.Notification.Aws.Settings;
using Bridge.Commons.Notification.Exceptions;
using Bridge.Commons.Notification.Sms.Contracts;
using Bridge.Commons.Notification.Sms.Models;

namespace Bridge.Commons.Notification.Aws.Sms
{
    public class AwsSmsService : ISmsService
    {
        private AmazonSimpleNotificationServiceClient _client;

        public AwsSmsService(AWSCredentials awsCredentials, RegionEndpoint regionEndpoint)
        {
            if (awsCredentials == null)
                throw new ArgumentNullException();
            _client = new AmazonSimpleNotificationServiceClient(awsCredentials, regionEndpoint);
        }

        public AwsSmsService(AwsCredentials awsCredentials)
        {
            if (string.IsNullOrWhiteSpace(awsCredentials.AccessKey))
            {
                _client = new AmazonSimpleNotificationServiceClient(awsCredentials.Endpoint);
            }
            else
            {
                var credentials = new BasicAWSCredentials(awsCredentials.AccessKey, awsCredentials.SecretKey);
                _client = new AmazonSimpleNotificationServiceClient(credentials, awsCredentials.Endpoint);
            }
        }

        public void Dispose()
        {
            _client?.Dispose();
        }

        public void Send(SmsMessage message)
        {
            Validate(message);

            var request = ConvertToPublishRequest(message);

            _client.PublishAsync(request).Wait();
        }

        public async Task SendAsync(SmsMessage message)
        {
            Validate(message);

            var request = ConvertToPublishRequest(message);

            await _client.PublishAsync(request);
        }

        public void Validate(SmsMessage message)
        {
            if (message.Sender.Length + message.Message.Length > 159)
                throw new SmsTooLongException();
            if (string.IsNullOrWhiteSpace(message.PhoneNumber) || string.IsNullOrWhiteSpace(message.Message))
                throw new ArgumentNullException();
        }

        private PublishRequest ConvertToPublishRequest(SmsMessage message)
        {
            var request = new PublishRequest
            {
                Message = $"{message.Sender}: {message.Message}",
                PhoneNumber = message.PhoneNumber
            };

            request.MessageAttributes.Add("AWS.SNS.SMS.SenderID",
                new MessageAttributeValue { DataType = "String", StringValue = message.Sender });

            return request;
        }

        ~AwsSmsService()
        {
            _client = null;
        }
    }
}