# AwsManager
Api Based Aws Instance Creator.


Sample Request for usage ;

1 - 'api/instance/create' --> This method is create one instance.
{
	"SecretKey":"xxx",
	"AccessKey":"xxx",
	"InitCommand":"ls",
	"RegionName":"us-east-1",
	"InstanceType":"t2.micro",
	"InstanceCount":1
}

2 - 'api/instance/createbulk' --> This method is create multi instance to specified region.
{
	  "SecretKey":"xxx",
	  "AccessKey":"xxx",
	  "InitCommand":"ls",
    "Regions":[
        {
            "Name": "ap-southeast-1",
            "Instances":[
                {
                    "Type": "c3.8xlarge",
                    "Count": 1
                },
                {
                    "Type": "c3.4xlarge",
                    "Count": 1
                }
            ]
        }
    ]
}
