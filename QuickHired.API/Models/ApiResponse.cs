using QuickHired.API.Interfaces;
using System.Net;

namespace QuickHired.API.Models
{
    public class ApiResponse<T> : IApiResponse
    {
        public bool IsError { get; set; }
        public HttpStatusCode Status { get; set; }
        public string Message { get; set; }
        public double ExecutionTime { get; set; }
        public T Model { get; set; }
        public ApiResponse()
        {
            Model = default;
            IsError = false;
            Status = HttpStatusCode.OK;
            ExecutionTime = 0;
            Message = string.Empty;
        }
        public ApiResponse(T model, bool isError, HttpStatusCode status,
            string message, double executionTime)
        {
            Model = model;
            IsError = isError;
            Status = status;
            Message = message;
            ExecutionTime = executionTime;
        }
    }
}
