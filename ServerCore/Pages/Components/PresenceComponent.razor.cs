using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.JSInterop;
using ServerCore.DataModel;
using ServerCore.ServerMessages;

namespace ServerCore.Pages.Components
{
    public class PresenceModel
    {
        public int UserId { get; set; }
        public string Name { get; set; }
        public PresenceType PresenceType { get; set; }
    }

    /// <summary>
    /// Live widget that shows which users are active on a puzzle page
    /// </summary>
    public partial class PresenceComponent : IAsyncDisposable
    {
        public string PresentUsersText;

        [Parameter]
        public int PuzzleUserId { get; set; }

        [Parameter]
        public int TeamId { get; set; }

        [Parameter]
        public int? PuzzleId { get; set; }

        [Parameter]
        public int? EventId { get; set; }

        /// <summary>
        /// True if the component will only read the presence and not write.
        /// </summary>
        [Parameter]
        public bool IsReadOnly { get; set; }

        /// <summary>
        /// True if we only want to show the presence and no other UI.
        /// </summary>
        [Parameter]
        public bool ShowPresenceOnly { get; set; }

        /// <summary>
        /// Max number of users to show.
        /// If more than this, then replace with the count (e.g. "3+")
        /// </summary>
        [Parameter]
        public int? MaxUsers { get; set; }

        Guid pageInstance = Guid.NewGuid();
        List<TeamPuzzleStore> teamPuzzleStores = new List<TeamPuzzleStore>();

        private async Task OnPresenceChange(int puzzleId, IDictionary<Guid, PresenceModel> presentPages)
        {
            await UpdateModelAsync(puzzleId, presentPages);
        }

        private async Task UpdateModelAsync(int puzzleId, IDictionary<Guid, PresenceModel> presentPages)
        {
            string presentUsersText = string.Empty;

            if (presentPages.Count > 0)
            {
                var deduplicatedUsers = from model in presentPages.Values
                                         group model by model.UserId into userGroup
                                         select new { UserId = userGroup.Key, PresenceType = userGroup.Min(user => user.PresenceType) };

                List<PresenceModel> presentUsers = new List<PresenceModel>();
                foreach(var user in deduplicatedUsers)
                {
                    PresenceModel presenceModel = new PresenceModel { UserId = user.UserId, PresenceType = user.PresenceType };
                    presenceModel.Name = await GetUserNameAsync(user.UserId);
                    presentUsers.Add(presenceModel);
                }

                presentUsers = presentUsers
                    .OrderBy(presence => presence.PresenceType)
                    .ThenBy(presence => presence.Name)
                    .ToList();

                if(MaxUsers.HasValue && presentUsers.Count > MaxUsers.Value)
                {
                    int remainingUsers = presentUsers.Count - MaxUsers.Value + 1;
                    string remainingUsersString = $"{remainingUsers}+";
                    presentUsersText = string.Join(" | ", presentUsers.Take(MaxUsers.Value - 1).Select(u => u.Name)) + " | " + remainingUsersString;
                }
                   else
                {
                    presentUsersText = string.Join(" | ", presentUsers.Select(u => u.Name));
                }
            }

            if (PuzzleId.HasValue)
            {
                PresentUsersText = presentUsersText;
                await InvokeAsync(StateHasChanged);
            }
            else
            {
                await this.JS.InvokeVoidAsync("showPresence", puzzleId, presentUsersText);
            }
        }

        /// <summary>
        /// Gets a puzzle user's name
        /// </summary>
        /// <param name="puzzleUserId"></param>
        /// <returns></returns>
        private async Task<string> GetUserNameAsync(int puzzleUserId)
        {
            string userName = await MemoryCache.GetOrCreateAsync<string>(puzzleUserId, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
                string userName = await (from user in PuzzleServerContext.PuzzleUsers
                                         where user.ID == puzzleUserId
                                         select user.Name).SingleAsync();
                if (userName is null)
                {
                    userName = String.Empty;
                }

                entry.SetValue(userName);
                entry.SetSize(userName.Length);
                return userName;
            });

            return userName;
        }

        protected override async Task OnInitializedAsync()
        {
            await MessageListener.EnsureInitializedAsync();
            await base.OnInitializedAsync();
        }

        private async Task TrackPuzzleIdAsync(int puzzleId)
        {
            var teamPuzzleStore = PresenceStore.GetOrCreateTeamPuzzleStore(TeamId, puzzleId);
            teamPuzzleStore.OnTeamPuzzlePresenceChange += OnPresenceChange;

            await UpdateModelAsync(puzzleId, teamPuzzleStore.PresentPages);

            if (!this.IsReadOnly)
            {
                await MessageHub.BroadcastPresenceMessageAsync(new PresenceMessage { PageInstance = pageInstance, PuzzleUserId = PuzzleUserId, TeamId = TeamId, PuzzleId = puzzleId, PresenceType = PresenceType.Active });
            }
        }

        protected override async Task OnParametersSetAsync()
        {
            if (PuzzleId.HasValue)
            {
                await TrackPuzzleIdAsync(PuzzleId.Value);
            }
            else
            {
                var puzzlesInEventQ = PuzzleServerContext.Puzzles.Where(puzzle => puzzle.Event.ID == EventId && puzzle.IsPuzzle && !puzzle.IsForSinglePlayer);
                var stateForTeamQ = PuzzleServerContext.PuzzleStatePerTeam.Where(state => state.TeamID == TeamId && state.UnlockedTime != null);
                var visiblePuzzleIds = await (from Puzzle puzzle in puzzlesInEventQ
                                      join PuzzleStatePerTeam pspt in stateForTeamQ on puzzle.ID equals pspt.PuzzleID
                                      select puzzle.ID).ToListAsync();

                foreach (int puzzleId in visiblePuzzleIds)
                {
                    await TrackPuzzleIdAsync(puzzleId);
                }
            }
            await base.OnParametersSetAsync();
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var teamPuzzleStore in teamPuzzleStores)
            {
                teamPuzzleStore.OnTeamPuzzlePresenceChange -= OnPresenceChange;
            }

            if (!this.IsReadOnly && PuzzleId.HasValue)
            {
                await MessageHub.BroadcastPresenceMessageAsync(new PresenceMessage { PageInstance = pageInstance, PuzzleUserId = PuzzleUserId, TeamId = TeamId, PuzzleId = PuzzleId.Value, PresenceType = PresenceType.Disconnected });
            }
        }
    }
}
