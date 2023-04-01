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

using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Models;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Entities;

namespace Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Repositories;

public interface ITechnicalUserProfileRepository
{
    /// <summary>
    /// Gets the profile offer data for the given offer id and user
    /// </summary>
    /// <param name="offerId">Id of the offer</param>
    /// <param name="iamUserId">The iam user id</param>
    /// <returns>Returns the offer profile data</returns>
    Task<OfferProfileData?> GetOfferProfileData(Guid offerId, string iamUserId);

    /// <summary>
    /// Creates the technical user profile for an offer
    /// </summary>
    /// <param name="id">Id of the technical user profile</param>
    /// <param name="offerId">Id of the offer</param>
    TechnicalUserProfile CreateTechnicalUserProfiles(Guid id, Guid offerId);

    /// <summary>
    /// Creates and deletes the technical user profile assigned roles
    /// </summary>
    /// <param name="technicalUserProfileId">Id of the technical user profile</param>
    /// <param name="initialUserRoles">the initial roles</param>
    /// <param name="modifyUserRoles">the new set of roles</param>
    void CreateDeleteTechnicalUserProfileAssignedRoles(Guid technicalUserProfileId, IEnumerable<Guid> initialUserRoles, IEnumerable<Guid> modifyUserRoles);

    /// <summary>
    /// Removes the technical user profiles and the assigned roles
    /// </summary>
    /// <param name="profilesToDelete">The profiles with there assigned userRoles</param>
    void RemoveTechnicalUserProfileWithAssignedRoles(IEnumerable<(Guid TechnicalUserProfileId, IEnumerable<Guid> UserRoleIds)> profilesToDelete);

    /// <summary>
    /// Gets the technical user profiles for a given offer
    /// </summary>
    /// <param name="offerId">Id of the offer</param>
    /// <param name="iamUserId">Id of the iam user</param>
    /// <returns>List of the technical user profile information</returns>
    Task<List<(bool IsUserOfProvidingCompany, TechnicalUserProfileInformation Information)>> GetTechnicalUserProfileInformation(Guid offerId, string iamUserId);
}
