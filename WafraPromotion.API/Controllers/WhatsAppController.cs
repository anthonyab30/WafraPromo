using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using System.ComponentModel.DataAnnotations;
using WafraPromotion.API.Data;
using WafraPromotion.API.Models;
using WafraPromotion.API.Services; // Added for ImageProcessingService
using Microsoft.Extensions.Logging; // Added for ILogger

namespace WafraPromotion.API.Controllers
{
    [Route("api/whatsapp")]
    [ApiController]
    public class WhatsAppController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly ImageProcessingService _imageProcessingService; // Added
        private readonly ILogger<WhatsAppController> _logger; // Added
        private static readonly string ImageUploadPath = Path.Combine("UploadedImages");

        public WhatsAppController(
            ApplicationDbContext context,
            IWebHostEnvironment hostingEnvironment,
            ImageProcessingService imageProcessingService, // Added
            ILogger<WhatsAppController> logger) // Added
        {
            _context = context;
            _hostingEnvironment = hostingEnvironment;
            _imageProcessingService = imageProcessingService; // Added
            _logger = logger; // Added

            var physicalUploadPath = Path.Combine(_hostingEnvironment.ContentRootPath, ImageUploadPath);
            if (!Directory.Exists(physicalUploadPath))
            {
                Directory.CreateDirectory(physicalUploadPath);
            }
        }

        [HttpPost("message")]
        public async Task<IActionResult> ReceiveMessage([FromBody] WhatsAppMessage message)
        {
            if (message == null || string.IsNullOrWhiteSpace(message.PhoneNumber) || string.IsNullOrWhiteSpace(message.TextCode))
            {
                return BadRequest("Phone number and code are required.");
            }

            var today = DateTime.UtcNow.Date;
            var existingEntryToday = await _context.Entries!
                .AnyAsync(e => e.PhoneNumber == message.PhoneNumber && e.Timestamp.Date == today);

            if (existingEntryToday)
            {
                _logger.LogWarning($"Duplicate entry attempt for phone {message.PhoneNumber} on {today.ToShortDateString()}.");
                return Conflict("An entry for this phone number has already been submitted today. Please try again tomorrow.");
            }

            var partialEntry = new Entry
            {
                PhoneNumber = message.PhoneNumber,
                Code = message.TextCode,
                Timestamp = DateTime.UtcNow,
                ImagePath = null
            };
            _context.Entries!.Add(partialEntry);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Partial entry created for phone {message.PhoneNumber} with code {message.TextCode}. ID: {partialEntry.Id}");
            return Ok(new { Message = $"Thank you for sending your code: {message.TextCode}. Please upload a photo of the pack showing the code.", EntryId = partialEntry.Id });
        }

        [HttpPost("image")]
        public async Task<IActionResult> ReceiveImage([FromForm] WhatsAppImageUpload model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.PhoneNumber) || model.ImageFile == null || model.ImageFile.Length == 0)
            {
                return BadRequest("Phone number and image file are required.");
            }

            var today = DateTime.UtcNow.Date;
            Entry? entryToUpdate = null;

            // Scenario 1: User provides an EntryId (from ReceiveMessage response)
            if (model.EntryId.HasValue)
            {
                entryToUpdate = await _context.Entries!
                    .FirstOrDefaultAsync(e => e.Id == model.EntryId.Value && e.PhoneNumber == model.PhoneNumber && e.ImagePath == null);
                if (entryToUpdate == null)
                {
                    _logger.LogWarning($"EntryId {model.EntryId} not found or already processed for phone {model.PhoneNumber}.");
                    // Don't fail yet, try to find by phone number if no code submitted with image
                }
            }

            // Scenario 2: No EntryId, or EntryId not found. Try to find a partial entry for today.
            if (entryToUpdate == null)
            {
                entryToUpdate = await _context.Entries!
                    .Where(e => e.PhoneNumber == model.PhoneNumber && e.Timestamp.Date == today && e.ImagePath == null)
                    .OrderByDescending(e => e.Timestamp)
                    .FirstOrDefaultAsync();
            }

            // If we found an entry to update, check if it already has an image (double submission for same partial entry)
            if (entryToUpdate != null && !string.IsNullOrEmpty(entryToUpdate.ImagePath)) {
                _logger.LogWarning($"Image already submitted for entry ID {entryToUpdate.Id} for phone {model.PhoneNumber}.");
                return Conflict("An image for this entry has already been submitted.");
            }

            // If still no entry to update, and no code submitted with the image, this is an issue.
            if (entryToUpdate == null && string.IsNullOrWhiteSpace(model.SubmittedCode))
            {
                _logger.LogWarning($"No matching partial entry found for phone {model.PhoneNumber} and no code provided with image.");
                return BadRequest("No prior code submission found for this phone number today. Please submit the code with the image, or send the code first.");
            }

            // Daily limit check for COMPLETED entries (has image path)
            var existingCompletedEntryToday = await _context.Entries!
                .AnyAsync(e => e.PhoneNumber == model.PhoneNumber && e.Timestamp.Date == today && !string.IsNullOrEmpty(e.ImagePath) && (entryToUpdate == null || e.Id != entryToUpdate.Id));

            if (existingCompletedEntryToday)
            {
                 _logger.LogWarning($"A completed entry (with image) already exists for phone {model.PhoneNumber} today.");
                 return Conflict("An image for this phone number has already been submitted and processed today. Please try again tomorrow.");
            }

            string imageFileName;
            string imagePathFullPhysical;
            string imagePathRelative;

            try
            {
                var physicalUploadDir = Path.Combine(_hostingEnvironment.ContentRootPath, ImageUploadPath);
                if (!Directory.Exists(physicalUploadDir))
                {
                    Directory.CreateDirectory(physicalUploadDir);
                }

                imageFileName = $"{Guid.NewGuid()}_{Path.GetFileName(model.ImageFile.FileName)}"; // Sanitize filename
                imagePathFullPhysical = Path.Combine(physicalUploadDir, imageFileName);
                imagePathRelative = Path.Combine(ImageUploadPath, imageFileName);


                using (var stream = new FileStream(imagePathFullPhysical, FileMode.Create))
                {
                    await model.ImageFile.CopyToAsync(stream);
                }
                _logger.LogInformation($"Image saved for phone {model.PhoneNumber} to {imagePathFullPhysical}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error saving image for phone {model.PhoneNumber}.");
                return StatusCode(StatusCodes.Status500InternalServerError, $"Error saving image: {ex.Message}");
            }

            bool isNewEntry = false;
            if (entryToUpdate == null) // Must be creating new entry because code was provided with image
            {
                entryToUpdate = new Entry
                {
                    PhoneNumber = model.PhoneNumber,
                    Code = model.SubmittedCode, // Code submitted with image
                    Timestamp = DateTime.UtcNow
                };
                isNewEntry = true;
            }

            entryToUpdate.ImagePath = imagePathRelative;
            if (!string.IsNullOrWhiteSpace(model.SubmittedCode) && string.IsNullOrWhiteSpace(entryToUpdate.Code))
            {
                 entryToUpdate.Code = model.SubmittedCode; // If partial entry had no code or new entry
            }
            if (string.IsNullOrWhiteSpace(entryToUpdate.Code))
            {
                 // This should ideally not happen if logic above is correct
                 _logger.LogError($"Entry ID {entryToUpdate.Id} for phone {model.PhoneNumber} has no code after image upload prep.");
                 System.IO.File.Delete(imagePathFullPhysical); // Cleanup orphaned image
                 return BadRequest("Critical error: Code is missing for the entry. Please resubmit code and image.");
            }


            // Process image using Python script
            _logger.LogInformation($"Processing image for entry ID {entryToUpdate.Id} (Phone: {entryToUpdate.PhoneNumber}) at path: {imagePathFullPhysical}");
            var processingResult = await _imageProcessingService.ProcessImageAsync(imagePathFullPhysical);

            if (processingResult == null || !string.IsNullOrEmpty(processingResult.Error))
            {
                _logger.LogError($"Image processing failed for {imagePathFullPhysical}. Error: {processingResult?.Error}");
                // Decide if we save entry without OCR/pHash or reject
                // For now, save with nulls, but flag or handle later
                entryToUpdate.OcrCode = "PROCESSING_ERROR";
            }
            else
            {
                entryToUpdate.OcrCode = processingResult.OcrText;
                entryToUpdate.ImageHash = processingResult.Phash;
                _logger.LogInformation($"Image processed for entry ID {entryToUpdate.Id}. OCR: '{processingResult.OcrText}', pHash: '{processingResult.Phash}'");

                if(!string.IsNullOrEmpty(processingResult.OcrError))
                {
                    _logger.LogWarning($"OCR processing for entry ID {entryToUpdate.Id} encountered an issue: {processingResult.OcrError}");
                }
                 if(!string.IsNullOrEmpty(processingResult.PhashError))
                {
                    _logger.LogWarning($"pHash generation for entry ID {entryToUpdate.Id} encountered an issue: {processingResult.PhashError}");
                }

                // 5. Compare submitted code with OCR result
                // Normalize codes for comparison (e.g., uppercase, remove spaces/hyphens if necessary)
                var submittedCodeNormalized = entryToUpdate.Code?.Trim().ToUpperInvariant() ?? "";
                var ocrCodeNormalized = (processingResult.OcrText ?? "").Trim().ToUpperInvariant();

                if (submittedCodeNormalized != ocrCodeNormalized)
                {
                    _logger.LogWarning($"Code mismatch for entry ID {entryToUpdate.Id}. Submitted: '{submittedCodeNormalized}', OCR: '{ocrCodeNormalized}'.");
                    // TODO: Add specific handling - e.g., flag entry, or if strict, delete image & reject.
                    // For now, we'll save it and it can be reviewed by admin.
                    // entryToUpdate.Status = "RequiresReview_CodeMismatch";
                }

                // 6. Check for duplicate image hash (if pHash was generated)
                if (!string.IsNullOrEmpty(entryToUpdate.ImageHash))
                {
                    var duplicateHashEntry = await _context.Entries!
                        .FirstOrDefaultAsync(e => e.ImageHash == entryToUpdate.ImageHash && e.Id != entryToUpdate.Id);
                    if (duplicateHashEntry != null)
                    {
                        _logger.LogWarning($"Duplicate image hash '{entryToUpdate.ImageHash}' detected for entry ID {entryToUpdate.Id}. Original entry ID: {duplicateHashEntry.Id} by phone {duplicateHashEntry.PhoneNumber}.");
                        // TODO: Add specific handling - e.g., flag entry, or if strict, delete image & reject.
                        // entryToUpdate.Status = "RequiresReview_DuplicateImage";
                    }
                }
            }

            if (isNewEntry)
            {
                _context.Entries!.Add(entryToUpdate);
            }
            else
            {
                _context.Entries!.Update(entryToUpdate);
            }
            await _context.SaveChangesAsync();
            _logger.LogInformation($"Entry ID {entryToUpdate.Id} (Phone: {entryToUpdate.PhoneNumber}) successfully updated/created with image and processing results.");

            return Ok(new { Message = "Thank you! Your photo has been uploaded and is being processed.", EntryId = entryToUpdate.Id });
        }
    }

    public class WhatsAppMessage
    {
        public string? PhoneNumber { get; set; }
        public string? TextCode { get; set; }
    }

    public class WhatsAppImageUpload
    {
        [Required]
        public string? PhoneNumber { get; set; }
        public int? EntryId { get; set; } // Optional: To link image to a prior message
        public string? SubmittedCode { get; set; }
        [Required]
        public IFormFile? ImageFile { get; set; }
    }
}
