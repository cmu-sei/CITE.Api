// Copyright 2024 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using System;
using Microsoft.AspNetCore.Http;

namespace Cite.Api.ViewModels
{
    public class FileForm
    {
        public IFormFile ToUpload { get; set; }
    }
}