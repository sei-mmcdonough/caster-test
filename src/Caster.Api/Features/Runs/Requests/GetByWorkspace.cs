/*
Crucible
Copyright 2020 Carnegie Mellon University.
NO WARRANTY. THIS CARNEGIE MELLON UNIVERSITY AND SOFTWARE ENGINEERING INSTITUTE MATERIAL IS FURNISHED ON AN "AS-IS" BASIS. CARNEGIE MELLON UNIVERSITY MAKES NO WARRANTIES OF ANY KIND, EITHER EXPRESSED OR IMPLIED, AS TO ANY MATTER INCLUDING, BUT NOT LIMITED TO, WARRANTY OF FITNESS FOR PURPOSE OR MERCHANTABILITY, EXCLUSIVITY, OR RESULTS OBTAINED FROM USE OF THE MATERIAL. CARNEGIE MELLON UNIVERSITY DOES NOT MAKE ANY WARRANTY OF ANY KIND WITH RESPECT TO FREEDOM FROM PATENT, TRADEMARK, OR COPYRIGHT INFRINGEMENT.
Released under a MIT (SEI)-style license, please see license.txt or contact permission@sei.cmu.edu for full terms.
[DISTRIBUTION STATEMENT A] This material has been approved for public release and unlimited distribution.  Please see Copyright notice for non-US Government use and distribution.
Carnegie Mellon(R) and CERT(R) are registered in the U.S. Patent and Trademark Office by Carnegie Mellon University.
DM20-0181
*/

using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using AutoMapper;
using Caster.Api.Data;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using AutoMapper.QueryableExtensions;
using System.Runtime.Serialization;
using Caster.Api.Infrastructure.Exceptions;
using Caster.Api.Domain.Models;
using System.Security.Claims;
using System.Security.Principal;
using Microsoft.AspNetCore.Authorization;
using Caster.Api.Infrastructure.Authorization;
using Caster.Api.Infrastructure.Identity;
using System.Text.Json.Serialization;

namespace Caster.Api.Features.Runs
{
    public class GetByWorkspace
    {
        [DataContract(Name="GetRunsByWorkspaceQuery")]
        public class Query : RunQuery, IRequest<Run[]>
        {
            /// <summary>
            /// The Id of the Workspace whose Runs to retrieve
            /// </summary>
            [JsonIgnore]
            public Guid WorkspaceId { get; set; }

            /// <summary>
            /// Limit the number of results returned to this amount if present
            /// </summary>
            public int? Limit { get; set; }
        }

        public class Handler : IRequestHandler<Query, Run[]>
        {
            private readonly CasterContext _db;
            private readonly IMapper _mapper;
            private readonly IAuthorizationService _authorizationService;
            private readonly ClaimsPrincipal _user;

            public Handler(
                CasterContext db,
                IMapper mapper,
                IAuthorizationService authorizationService,
                IIdentityResolver identityResolver)
            {
                _db = db;
                _mapper = mapper;
                _authorizationService = authorizationService;
                _user = identityResolver.GetClaimsPrincipal();
            }

            public async Task<Run[]> Handle(Query request, CancellationToken cancellationToken)
            {
                if (!(await _authorizationService.AuthorizeAsync(_user, null, new ContentDeveloperRequirement())).Succeeded)
                    throw new ForbiddenException();

                await ValidateWorkspace(request.WorkspaceId);

                return await _db.Runs
                    .Where(x => x.WorkspaceId == request.WorkspaceId)
                    .OrderByDescending(r => r.CreatedAt)
                    .Limit(request.Limit)
                    .Expand(_mapper.ConfigurationProvider,
                            includePlan: request.IncludePlan,
                            includeApply: request.IncludeApply)
                    .ToArrayAsync();
            }

            private async Task ValidateWorkspace(Guid workspaceId)
            {
                var workspace = await _db.Workspaces.FindAsync(workspaceId);

                if (workspace == null)
                    throw new EntityNotFoundException<Workspace>();
            }
        }
    }
}

