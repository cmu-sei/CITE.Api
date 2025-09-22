// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using System;

namespace Cite.Api.ViewModels
{
    public class DutyUser : Base
    {
        public DutyUser() { }

        public DutyUser(Guid userId, Guid dutyId)
        {
            UserId = userId;
            DutyId = dutyId;
        }

        public Guid Id { get; set; }

        public Guid UserId { get; set; }
        public User User { get; set; }

        public Guid DutyId { get; set; }
        public Duty Duty { get; set; }
    }

}
