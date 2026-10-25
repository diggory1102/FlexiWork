using System;

namespace App.Domain.Entities
{
    /// <summary>
    /// Vector khuôn mặt Face ID phục vụ đối soát sinh trắc học
    /// </summary>
    public class FaceProfile : BaseEntity
    {
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        /// <summary>
        /// Mảng vector 128 hoặc 512 chiều được ánh xạ từ JSON trong MySQL
        /// </summary>
        public string FaceEncoding { get; set; } = string.Empty;

        public string SampleImageUrl { get; set; } = string.Empty;
    }
}
