using System;
using System.ComponentModel.DataAnnotations;

namespace WafraPromotion.API.Models
{
    public class Entry
    {
        public int Id { get; set; }

        [Required]
        [Phone]
        public string? PhoneNumber { get; set; }

        [Required]
        public string? Code { get; set; }

        [Required]
        public string? ImagePath { get; set; } // Store path to the image

        public string? ImageHash { get; set; } // Perceptual hash of the image

        public string? OcrCode { get; set; } // Code extracted by OCR

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
