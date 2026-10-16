using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using QuickHired.API.Models;
using System.Diagnostics;
using System.Net;

namespace QuickHired.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ControllerHandler : ControllerBase
    {
        public ApiResponse<T> DoTry<T>(Func<T> action)
        {
            var result = new ApiResponse<T>();
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            try
            {
                T model = action();
                result.Model = model;
            }
            catch (UnauthorizedAccessException ex)
            {
                result.IsError = true;
                result.Status = HttpStatusCode.Unauthorized;
                result.Message = ex.Message.ToString();
            }
            catch (Exception ex)
            {
                result.IsError = true;
                result.Status = HttpStatusCode.InternalServerError;
                result.Message = ex.Message.ToString();
            }
            finally
            {
                stopWatch.Stop();
                result.ExecutionTime = stopWatch.Elapsed.TotalSeconds;
            }
            return result;
        }
        public async Task<ApiResponse<T>> DoTryAsync<T>(Func<Task<T>> action,
                                                    string controllerName,
                                                    string actionName,
                                                    ILogger logger)
        {
            Func<T> syncAction = () => action().GetAwaiter().GetResult();
            return await Task.Run(() =>
            {
                var result = DoTry(syncAction);
                if (result.IsError)
                {
                    logger.LogError("An error occurred in controller {ControllerName}, action {ActionName}: {ErrorMessage}",
                                    controllerName, actionName, result.Message);

                }
                else
                {
                    logger.LogInformation("{ControllerName} - {ActionName}() executed successfully.",
                                          controllerName, actionName);
                }
                return result;
            });
        }
    }
}
