﻿using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServerCore.DataModel;
using ServerCore.ModelBases;

namespace ServerCore.Pages.Puzzles
{
    public class StatusModel : PuzzleStatePerTeamPageModel
    {
        public StatusModel(ServerCore.Models.PuzzleServerContext context) : base(context)
        {
        }

        public Puzzle Puzzle { get; set; }

        protected override string DefaultSort => "team";

        public async Task<IActionResult> OnGetAsync(int id, string sort)
        {
            Puzzle = await Context.Puzzles.FirstOrDefaultAsync(m => m.ID == id);

            if (Puzzle == null)
            {
                return NotFound();
            }

            await InitializeModelAsync(puzzleId: id, teamId: null, sort: sort);
            return Page();
        }

        public async Task<IActionResult> OnGetUnlockStateAsync(int id, int? teamId, bool value, string sort)
        {
            await SetUnlockStateAsync(puzzleId: id, teamId: teamId, value: value);

            // redirect without the unlock info to keep the URL clean
            return RedirectToPage(new { id, sort });
        }

        public async Task<IActionResult> OnGetSolveStateAsync(int id, int? teamId, bool value, string sort)
        {
            await SetSolveStateAsync(puzzleId: id, teamId: teamId, value: value);

            // redirect without the solve info to keep the URL clean
            return RedirectToPage(new { id, sort });
        }
    }
}
