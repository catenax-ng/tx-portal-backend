/********************************************************************************
 * Copyright (c) 2021, 2023 BMW Group AG
 * Copyright (c) 2021, 2023 Contributors to the Eclipse Foundation
 *
 * See the NOTICE file(s) distributed with this work for additional
 * information regarding copyright ownership.
 *
 * This program and the accompanying materials are made available under the
 * terms of the Apache License, Version 2.0 which is available at
 * https://www.apache.org/licenses/LICENSE-2.0.
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
 * License for the specific language governing permissions and limitations
 * under the License.
 *
 * SPDX-License-Identifier: Apache-2.0
 ********************************************************************************/

using Microsoft.EntityFrameworkCore;
using Org.Eclipse.TractusX.Portal.Backend.Framework.DBAccess;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Models;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Entities;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Enums;

namespace Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Repositories;

public class TechnicalUserProfileRepository : ITechnicalUserProfileRepository
{
    private readonly PortalDbContext _context;

    public TechnicalUserProfileRepository(PortalDbContext dbContext)
    {
        _context = dbContext;
    }
    
    /// <inheritdoc />
    public Task<OfferProfileData?> GetOfferProfileData(Guid offerId, string iamUserId) =>
        _context.Offers
            .Where(x => x.Id == offerId)
            .Select(o => new OfferProfileData(
                o.ProviderCompany!.CompanyUsers.Any(x => x.IamUser!.UserEntityId == iamUserId),
                o.OfferTypeId == OfferTypeId.SERVICE ? o.ServiceDetails.Select(sd => sd.ServiceTypeId) : new List<ServiceTypeId>(),
                o.TechnicalUserProfiles.Select(tup => new ValueTuple<Guid, IEnumerable<Guid>>(tup.Id, tup.UserRoles.Select(ur => ur.Id)))))
            .SingleOrDefaultAsync();

    /// <inheritdoc />
    public TechnicalUserProfile CreateTechnicalUserProfiles(Guid id, Guid offerId) => 
        _context.TechnicalUserProfiles.Add(new TechnicalUserProfile(id, offerId)).Entity;
    
    ///<inheritdoc/>
    public void CreateDeleteTechnicalUserProfileAssignedRoles(Guid technicalUserProfileId, IEnumerable<Guid> initialUserRoles, IEnumerable<Guid> modifyUserRoles) =>
        _context.AddRemoveRange(
            initialUserRoles,
            modifyUserRoles,
            userRoleId => new TechnicalUserProfileAssignedUserRole(technicalUserProfileId, userRoleId));

    /// <inheritdoc />
    public void RemoveTechnicalUserProfileWithAssignedRoles(IEnumerable<(Guid TechnicalUserProfileId, IEnumerable<Guid> UserRoleIds)> profilesToDelete)
    {
        _context.TechnicalUserProfileAssignedUserRoles.RemoveRange(profilesToDelete.SelectMany(p => p.UserRoleIds.Select(ur => new TechnicalUserProfileAssignedUserRole(p.TechnicalUserProfileId, ur))));
        _context.TechnicalUserProfiles.RemoveRange(profilesToDelete.Select(x => new TechnicalUserProfile(x.TechnicalUserProfileId, Guid.Empty)));
    }

    /// <inheritdoc />
    public Task<List<(bool IsUserOfProvidingCompany, TechnicalUserProfileInformation Information)>> GetTechnicalUserProfileInformation(Guid offerId, string iamUserId) =>
        _context.TechnicalUserProfiles
            .Where(x => x.OfferId == offerId)
            .Select(x => new ValueTuple<bool, TechnicalUserProfileInformation>(
                x.Offer!.ProviderCompany!.CompanyUsers.Any(x => x.IamUser!.UserEntityId == iamUserId),
                new TechnicalUserProfileInformation(
                    x.Id, 
                    x.UserRoles.Select(ur => new UserRoleInformation(ur.Id, ur.UserRoleText)))))
            .ToListAsync();
}
