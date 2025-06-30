using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WafraPromotion.API.Data;
using WafraPromotion.API.Models;

namespace WafraPromotion.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EntriesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public EntriesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/Entries
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Entry>>> GetEntries()
        {
            return await _context.Entries!.ToListAsync();
        }

        // GET: api/Entries/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Entry>> GetEntry(int id)
        {
            var entry = await _context.Entries!.FindAsync(id);

            if (entry == null)
            {
                return NotFound();
            }

            return entry;
        }

        // POST: api/Entries
        [HttpPost]
        public async Task<ActionResult<Entry>> PostEntry(Entry entry)
        {
            // Basic validation for now, more complex validation later
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            _context.Entries!.Add(entry);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetEntry), new { id = entry.Id }, entry);
        }

        // Placeholder for future admin actions
        // GET: api/Entries/filter?date=YYYY-MM-DD&code=XYZ&phone=123
        [HttpGet("filter")]
        public async Task<ActionResult<IEnumerable<Entry>>> FilterEntries([FromQuery] string? date, [FromQuery] string? code, [FromQuery] string? phone)
        {
            // This is a placeholder. Implementation will be added later.
            return await _context.Entries!.ToListAsync();
        }

        // Placeholder for future admin actions
        // GET: api/Entries/export
        [HttpGet("export")]
        public IActionResult ExportEntries()
        {
            // This is a placeholder. Implementation will be added later.
            return Ok("Export functionality to be implemented.");
        }

        // Placeholder for future admin actions
        // POST: api/Entries/draw
        [HttpPost("draw")]
        public async Task<ActionResult<Entry>> DrawWinner()
        {
            // This is a placeholder. Implementation will be added later.
            var winner = await _context.Entries!.FirstOrDefaultAsync(); // Simplistic draw
            if (winner == null)
            {
                return NotFound("No entries to draw from.");
            }
            return Ok(winner);
        }
    }
}
