using System.Net;

namespace QuickHired.API.Models
{
    public class ApiValidationResponse
    {
        public bool IsError { get; set; }
        public HttpStatusCode Status { get; set; }
        public string Message { get; set; }
        public double ExecutionTime { get; set; }
        public object Model { get; set; }
    }
}
