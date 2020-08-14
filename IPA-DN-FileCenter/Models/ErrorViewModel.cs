using System;

namespace IPA.DN.FileCenter.Models
{
    public class ErrorViewModel
    {
        public string RequestId { get; set; } = null!;

        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

        public Exception ErrorInfo { get; set; } = null!;
    }
}
