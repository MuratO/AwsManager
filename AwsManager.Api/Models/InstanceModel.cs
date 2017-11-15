using System.Collections.Generic;

namespace AwsManager.Api.Models
{
    public struct InstanceModel
    {
        public string SecretKey { get; set; }
        public string AccessKey { get; set; }
        public string RegionName { get; set; }
        public string InstanceType { get; set; }
        public int InstanceCount { get; set; }
        public string InitCommand { get; set; }
    }

    public struct BulkInstanceModel
    {
        public string SecretKey { get; set; }
        public string AccessKey { get; set; }
        public string MinerAccount { get; set; }
        public List<Region> Regions {get; set;} 
    }

    public struct Region
    {
        public string Name { get; set; }
        public List<Instance> Instances { get; set; } 
    }

    public struct Instance
    {
        public string Type { get; set; }
        public int Count { get; set; }
    }
}