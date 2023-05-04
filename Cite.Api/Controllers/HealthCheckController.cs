// Copyright 2022 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license, please see LICENSE.md in the project root for license information or contact permission@sei.cmu.edu for full terms.

using System.Reflection;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Cite.Api.Data;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;

namespace Cite.Api.Controllers
{
    [Route("api/")]
    [ApiController]
    public class HealthCheckController : ControllerBase
    {
        private readonly CiteContext _context;

        public HealthCheckController(
            CiteContext context
        )
        {
            _context = context;
        }

        /// <summary>
        /// Responds when this API is functional
        /// </summary>
        /// <remarks>
        /// Returns a health message, "It is well".
        /// </remarks>
        /// <returns></returns>
        [HttpGet("healthcheck")]
        [ProducesResponseType(typeof(string), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "healthCheck")]
        public async Task<IActionResult> HealthCheck(CancellationToken ct)
        {
            var healthMessage = "It is well";
            try
            {
                var dbCheck = await _context.Users.Select(g => g.Id).FirstAsync();
            }
            catch (System.Exception ex)
            {
                healthMessage = "I'm sorry, but I currently can't access the database.  " + ex.Message;
            }
            return Ok(healthMessage);
        }

        /// <summary>
        /// Returns the current version of the API
        /// </summary>
        /// <remarks>
        /// Returns the version.
        /// </remarks>
        /// <returns></returns>
        [HttpGet("version")]
        [ProducesResponseType(typeof(string), (int)HttpStatusCode.OK)]
        [SwaggerOperation(OperationId = "getVersion")]
        public async Task<IActionResult> GetVersion(CancellationToken ct)
        {
            var version = (AssemblyInformationalVersionAttribute)Assembly
                .GetExecutingAssembly()
                .GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false)
                .FirstOrDefault();

            return Ok(version.InformationalVersion);
        }

    }
}
