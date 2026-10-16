using System.Net;

namespace QuickHired.API.Interfaces
{
    public interface IApiResponse
    {
        public string Message { get; set; }
        public bool IsError { get; set; }
        public HttpStatusCode Status { get; set; }
        public double ExecutionTime { get; set; }
    }
}
