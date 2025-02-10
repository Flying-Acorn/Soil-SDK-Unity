namespace FlyingAcorn.Soil.Core.User.Authentication.Data
{
    public abstract class Notification
    {
        public string Message { get; set; }
        public string CaseId { get; set; }
        public string PlayerId { get; set; }
        public string ProjectId { get; set; }
        public string CreatedAt { get; set; }
        public string Id { get; set; }
    }
}