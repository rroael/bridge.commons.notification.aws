using Amazon;

namespace Bridge.Commons.Notification.Aws.Settings
{
    public class AwsCredentials
    {
        public string Region { get; set; }
        public string AccessKey { get; set; }
        public string SecretKey { get; set; }

        public RegionEndpoint Endpoint => RegionEndpoint.GetBySystemName(Region);
    }
}