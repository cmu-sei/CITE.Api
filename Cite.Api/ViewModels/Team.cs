// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using System;
using System.Collections.Generic;

namespace Cite.Api.ViewModels
{
    public class Team : Base, IAuthorizationType
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string ShortName { get; set; }
        public Guid? EvaluationId { get; set; }
        public Guid TeamTypeId { get; set; }
        public TeamType TeamType { get; set; }
        public ICollection<User> Users { get; set; } = new List<User>();
        public virtual ICollection<Submission> Submissions { get; set; } = new List<Submission>();
        public bool HideScoresheet { get; set; }
        public ICollection<TeamMembership> Memberships { get; set; } = new List<TeamMembership>();
    }

}
