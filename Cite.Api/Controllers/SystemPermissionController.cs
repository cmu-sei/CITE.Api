// Copyright 2024 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Cite.Api.Infrastructure.Authorization;
using Cite.Api.Data.Enumerations;

namespace Cite.Api.Controllers;

public class SystemPermissionsController : BaseController
{
    private readonly ICiteAuthorizationService _authorizationService;

    public SystemPermissionsController(ICiteAuthorizationService authorizationService)
    {
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// Get all SystemPermissions for the calling User.
    /// </summary>
    /// <returns></returns>
    [HttpGet("me/systemPermissions")]
    [ProducesResponseType(typeof(IEnumerable<SystemPermission>), (int)HttpStatusCode.OK)]
    [SwaggerOperation(OperationId = "GetMySystemPermissions")]
    public async Task<IActionResult> GetMine()
    {
        var result = _authorizationService.GetSystemPermissions().ToArray();
        return Ok(result);
    }
}
