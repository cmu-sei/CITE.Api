// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using System;

namespace Cite.Api.ViewModels
{
    public class Group : Base
    {
        public Guid Id { get; set; }

        public string Name { get; set; }

        public Team[] Teams { get; set; }

    }
}
