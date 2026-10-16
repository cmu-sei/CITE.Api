// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

namespace Cite.Api.ViewModels
{
    /// <summary>
    /// The only field editable on an existing duty. It has no evaluation or team, so an edit cannot move the duty.
    /// </summary>
    public class UpdateDutyRequest
    {
        public string Name { get; set; }
    }
}
