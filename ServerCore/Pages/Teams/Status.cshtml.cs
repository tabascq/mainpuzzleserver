﻿using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServerCore.DataModel;
using ServerCore.ModelBases;

namespace ServerCore.Pages.Teams
{
    /// <summary>
    /// Model for author/admin's team-centric Status page.
    /// used for tracking what each team's progress is and altering that progress manually if needed.
    /// An author's view should be filtered to puzzles where they are an author (NYI so far though).
    /// </summary>
    [Authorize(Policy = "IsEventAdminOrEventAuthor")]
    public class StatusModel : PuzzleStatePerTeamPageModel
    {
        public StatusModel(PuzzleServerContext serverContext, UserManager<IdentityUser> userManager) : base(serverContext, userManager)
        {
        }

        public Team Team { get; set; }

        protected override SortOrder DefaultSort => SortOrder.PuzzleAscending;

        public async Task<IActionResult> OnGetAsync(int id, SortOrder? sort)
        {
            Team = await _context.Teams.FirstOrDefaultAsync(m => m.ID == id);

            return await InitializeModelAsync(null, Team, sort: sort);
        }

        public async Task<IActionResult> OnGetUnlockStateAsync(int id, int? puzzleId, bool value, string sort)
        {
            if (EventRole != EventRole.admin && EventRole != EventRole.author)
            {
                return NotFound();
            }

            var puzzle = puzzleId == null ? null : await _context.Puzzles.FirstAsync(m => m.ID == puzzleId.Value);
            var team = await _context.Teams.FirstAsync(m => m.ID == id);

            await SetUnlockStateAsync(puzzle, team, value);

            // redirect without the unlock info to keep the URL clean
            return RedirectToPage(new { id, sort });
        }

        public async Task<IActionResult> OnGetSolveStateAsync(int id, int? puzzleId, bool value, string sort)
        {
            if (EventRole != EventRole.admin && EventRole != EventRole.author)
            {
                return NotFound();
            }

            var puzzle = puzzleId == null ? null : await _context.Puzzles.FirstAsync(m => m.ID == puzzleId.Value);
            var team = await _context.Teams.FirstAsync(m => m.ID == id);

            await SetSolveStateAsync(puzzle, team, value);

            // redirect without the solve info to keep the URL clean
            return RedirectToPage(new { id, sort });
        }
    }
}
