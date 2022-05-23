// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Cite.Api.Data.Enumerations;

namespace Cite.Api.ViewModels
{
    public class Role : Base
    {
        public Guid Id { get; set; }
        public Guid EvaluationId { get; set; }
        public virtual Evaluation Evaluation { get; set; }
        public Guid TeamId { get; set; }
        public virtual Team Team { get; set; }
        public string Name { get; set; }
        public ICollection<User> Users { get; set; } = new List<User>();
   }
}

