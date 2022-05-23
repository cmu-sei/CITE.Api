// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Cite.Api.ViewModels
{
    public class TeamType : Base
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
    }

}
