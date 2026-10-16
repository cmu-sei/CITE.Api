// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

namespace Cite.Api.ViewModels
{
    /// <summary>
    /// Fields editable on an existing action. It has no evaluation or team, so an edit cannot move the action. Whether it is checked changes only through check and uncheck.
    /// </summary>
    public class UpdateActionRequest
    {
        public int MoveNumber { get; set; }
        public int InjectNumber { get; set; }
        public int ActionNumber { get; set; }
        public string Description { get; set; }
    }
}
