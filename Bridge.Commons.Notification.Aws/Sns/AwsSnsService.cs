using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Bridge.Commons.Notification.Aws.Settings;
using Bridge.Commons.Notification.PushNotification.Contracts;
using Bridge.Commons.Notification.PushNotification.Models;
using Newtonsoft.Json;

namespace Bridge.Commons.Notification.Aws.Sns
{
    public class AwsSnsService : IPushNotificationService
    {
        private AmazonSimpleNotificationServiceClient _client;

        public AwsSnsService(AWSCredentials awsCredentials, RegionEndpoint regionEndpoint)
        {
            if (awsCredentials == null)
                throw new ArgumentNullException();
            _client = new AmazonSimpleNotificationServiceClient(awsCredentials, regionEndpoint);
        }

        public AwsSnsService(AwsCredentials awsCredentials)
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

        public async Task<string> RegisterUserEndpoint(PushNotificationRequest request)
        {
            var updateNeeded = false;
            var createNeeded = false;

            // No platform endpoint ARN is stored; need to call createEndpoint.
            var responseEndpoint = CreateEndpoint(request).Result;

            // Look up the platform endpoint and make sure the data in it is current, even if it was just created.
            try
            {
                var geaReq = new GetEndpointAttributesRequest { EndpointArn = responseEndpoint.EndpointArn };
                var geaRes = await _client.GetEndpointAttributesAsync(geaReq);
                updateNeeded = geaRes.Attributes["Token"] != request.DeviceToken ||
                               geaRes.Attributes["Enabled"] != "true";
            }
            catch (NotFoundException)
            {
                // We had a stored ARN, but the platform endpoint associated with it disappeared. Recreate it.
                createNeeded = true;
            }

            if (createNeeded)
                responseEndpoint = CreateEndpoint(request).Result;

            if (updateNeeded)
            {
                // The platform endpoint is out of sync with the current data; update the token and enable it.
                var attribs = new Dictionary<string, string> { ["Token"] = request.DeviceToken, ["Enabled"] = "true" };
                var saeReq = new SetEndpointAttributesRequest
                    { EndpointArn = responseEndpoint.EndpointArn, Attributes = attribs };
                var saeRes = await _client.SetEndpointAttributesAsync(saeReq);
            }

            return responseEndpoint.HttpStatusCode == HttpStatusCode.OK ? responseEndpoint.EndpointArn : string.Empty;
        }

        public async Task<bool> DeleteUserEndpoint(string endpoint)
        {
            var request = new DeleteEndpointRequest { EndpointArn = endpoint };

            var response = await _client.DeleteEndpointAsync(request);

            return response.HttpStatusCode == HttpStatusCode.OK;
        }

        public void Send(PushNotificationRequest message)
        {
            Validate(message);

            var request = ConvertToPublishRequest(message);

            _client.PublishAsync(request).Wait();
        }

        public async Task SendAsync(PushNotificationRequest request)
        {
            Validate(request);

            var publishRequest = ConvertToPublishRequest(request);

            await _client.PublishAsync(publishRequest);
        }

        public void Validate(PushNotificationRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Message)
                || string.IsNullOrWhiteSpace(request.EndpointArn)
                || request.IosDevice && string.IsNullOrWhiteSpace(request.PlatformEndpointForIos)
                || !request.IosDevice && string.IsNullOrWhiteSpace(request.PlatformEndpointForAndroid))
                throw new ArgumentNullException();
        }

        private async Task<CreatePlatformEndpointResponse> CreateEndpoint(PushNotificationRequest request)
        {
            var endpointRequest = new CreatePlatformEndpointRequest
            {
                Token = request.DeviceToken,
                PlatformApplicationArn =
                    request.IosDevice ? request.PlatformEndpointForIos : request.PlatformEndpointForAndroid
            };

            return await _client.CreatePlatformEndpointAsync(endpointRequest);
        }

        private PublishRequest ConvertToPublishRequest(PushNotificationRequest request)
        {
            var reqObj = new PushNotificationPush();
            if (request.IosDevice)
            {
                var apns = new APNS
                {
                    aps = new Aps
                    {
                        alert = new Alert { body = request.Message, title = request.Title },
                        contentAvailable = request.IsSilentPush ? 1 : 0
                    },
                    data = request.ExtraData
                };
                reqObj.APNS_SANDBOX = reqObj.APNS = JsonConvert.SerializeObject(apns);
            }
            else
            {
                var gcmData = new GCMData { data = new GCM { body = request.Message, title = request.Title } };
                var gcmNotification = new GCMNotification
                {
                    notification = new GCM { body = request.Message, title = request.Title }, data = request.ExtraData
                };
                reqObj.GCM = request.IsSilentPush
                    ? JsonConvert.SerializeObject(gcmData)
                    : JsonConvert.SerializeObject(gcmNotification);
            }

            var message = JsonConvert.SerializeObject(reqObj);

            var publish = new PublishRequest
            {
                TargetArn = request.EndpointArn,
                MessageStructure = "json",
                Message = message
            };

            return publish;
        }

        ~AwsSnsService()
        {
            _client = null;
        }
    }
}