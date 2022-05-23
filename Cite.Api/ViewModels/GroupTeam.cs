// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Cite.Api.ViewModels
{
    public class GroupTeam : Base
    {
        public GroupTeam() { }

        public GroupTeam(Guid groupId, Guid teamId)
        {
            GroupId = groupId;
            TeamId = teamId;
        }

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }

        public Guid GroupId { get; set; }
        public Group Group { get; set; }

        public Guid TeamId { get; set; }
        public Team Team { get; set; }
    }

}

