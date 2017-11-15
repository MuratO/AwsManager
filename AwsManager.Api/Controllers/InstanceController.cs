using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Description;
using AwsManager.Api.Models;

namespace AwsManager.Api.Controllers
{
    [RoutePrefix("api/Instance")]
    public class InstanceController : ApiController
    {
        public InstanceController()
        {
            
        }

        [Route("Create")]
        [ResponseType(typeof(ResponseModel))]
        public async Task<IHttpActionResult> Create(InstanceModel model)
        {
            var responseModel = new ResponseModel();

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var awsManager = new Core.AwsManager(model.SecretKey, model.AccessKey,model.RegionName);

               var response = await awsManager.CreateWorker(model.RegionName, model.InstanceType, model.InstanceCount, model.MinerAccount);
                responseModel.RegionName = model.RegionName;
                responseModel.Status = response;
                responseModel.InstanceType = model.InstanceType;

            }
            catch (Exception ex)
            {
                return InternalServerError();
            }
            
            return Ok(responseModel);
        }

        [Route("CreateBulk")]
        [ResponseType(typeof(IList<ResponseModel>))]
        public async Task<IHttpActionResult> CreateBulk(BulkInstanceModel model)
        {
             IList<ResponseModel> responseModel = new List<ResponseModel>();

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                foreach (var region in model.Regions)
                {

                    var awsManager = new Core.AwsManager(model.SecretKey, model.AccessKey, region.Name);

                    foreach (var instance in region.Instances)
                    {
                        var response = await awsManager.CreateWorker(region.Name, instance.Type, instance.Count, model.MinerAccount);

                        responseModel.Add(new ResponseModel()
                        {
                            RegionName = region.Name,
                            InstanceType = instance.Type,
                            Status = response
                        });
                    }
                }

            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }

            return Ok(responseModel);
        }

        [Route("GetInstances")]
        public List<string> GetInstances()
        {
            return new Core.AwsManager().GetInstanceList();
        }

        [Route("GetRegions")]
        public List<string> GetRegions()
        {
            return new Core.AwsManager().GetRegionList();
        }
    }
}
