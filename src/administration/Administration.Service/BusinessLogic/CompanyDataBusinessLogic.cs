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

using Org.Eclipse.TractusX.Portal.Backend.Administration.Service.Models;
using Org.Eclipse.TractusX.Portal.Backend.Framework.Async;
using Org.Eclipse.TractusX.Portal.Backend.Framework.ErrorHandling;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Repositories;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Models;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Enums;
using System.Net;

namespace Org.Eclipse.TractusX.Portal.Backend.Administration.Service.BusinessLogic;

public class CompanyDataBusinessLogic : ICompanyDataBusinessLogic
{
    private readonly IPortalRepositories _portalRepositories;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="portalRepositories"></param>
    public CompanyDataBusinessLogic(IPortalRepositories portalRepositories)
    {
        _portalRepositories = portalRepositories;
    }

    /// <inheritdoc/>
    public async Task<CompanyAddressDetailData> GetOwnCompanyDetailsAsync(string iamUserId)
    {
        var result = await _portalRepositories.GetInstance<ICompanyRepository>().GetOwnCompanyDetailsAsync(iamUserId).ConfigureAwait(false);
        if (result == null)
        {
            throw new ConflictException($"user {iamUserId} is not associated with any company");
        }
        return result;
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<CompanyAssignedUseCaseData> GetCompanyAssigendUseCaseDetailsAsync(string iamUserId) =>
        _portalRepositories.GetInstance<ICompanyRepository>().GetCompanyAssigendUseCaseDetailsAsync(iamUserId);

    /// <inheritdoc/>
    public async Task<HttpStatusCode> CreateCompanyAssignedUseCaseDetailsAsync(string iamUserId, Guid useCaseId)
    {
        var companyRepositories = _portalRepositories.GetInstance<ICompanyRepository>();
        var useCaseDetails = await companyRepositories.GetCompanyStatusAndUseCaseIdAsync(iamUserId, useCaseId).ConfigureAwait(false);
        if(!useCaseDetails.IsActiveCompanyStatus)
        {
            throw new ConflictException("Company Status is Incorrect");
        }
        if(useCaseDetails.IsUseCaseIdExists)
        {
            return HttpStatusCode.AlreadyReported;
        }
        companyRepositories.CreateCompanyAssignedUseCase(useCaseDetails.CompanyId, useCaseId);
        await _portalRepositories.SaveAsync().ConfigureAwait(false);
        return HttpStatusCode.NoContent;
    }

    /// <inheritdoc/>
    public async Task RemoveCompanyAssignedUseCaseDetailsAsync(string iamUserId, Guid useCaseId)
    {
        var companyRepositories = _portalRepositories.GetInstance<ICompanyRepository>();
        var useCaseDetails = await companyRepositories.GetCompanyStatusAndUseCaseIdAsync(iamUserId, useCaseId).ConfigureAwait(false);
        if(!useCaseDetails.IsActiveCompanyStatus)
        {
            throw new ConflictException("Company Status is Incorrect");
        }
        if(!useCaseDetails.IsUseCaseIdExists)
        {
            throw new ConflictException($"UseCaseId {useCaseId} is not available");
        }
        companyRepositories.RemoveCompanyAssignedUseCase(useCaseDetails.CompanyId, useCaseId);
        await _portalRepositories.SaveAsync().ConfigureAwait(false);
    }

    public IAsyncEnumerable<CompanyRoleConsentData> GetCompanyRoleAndConsentAgreementDetailsAsync(string iamUserId) =>
        _portalRepositories.GetInstance<ICompanyRepository>().GetCompanyRoleAndConsentAgreementDetailsAsync(iamUserId)
            .OnEmpty(() => throw new ConflictException($"user {iamUserId} is not associated with any company or Incorrect Status"));

    /// <inheritdoc/>
    public async Task CreateCompanyRoleAndConsentAgreementDetailsAsync(string iamUserId, IEnumerable<CompanyRoleConsentDetails> companyRoleConsentDetails)
    {
        var result = await _portalRepositories.GetInstance<ICompanyRepository>().GetCompanyRolesDataAsync(iamUserId).ConfigureAwait(false);
        if(result == default)
        {
            throw new NotFoundException($"user {iamUserId} is not associated with any company");
        }
        if(!result.IsCompanyActive)
        {
            throw new ConflictException("Company Status is Incorrect");
        }
        if (result.AgreementAssignedRoles == null || result.CompanyRoleIds == null || result.ConsentStatusDetails == null)
        {
            throw new UnexpectedConditionException("none of AgreementAssignedRoles, CompanyRoleIds, ConsentStatusDetails should ever be null here");
        }
        foreach(var data in companyRoleConsentDetails)
        {
            if(!result.AgreementAssignedRoles.Contains(data.CompanyRole) || data.Agreements.Any(x => x.ConsentStatus != ConsentStatusId.ACTIVE))
            {
                throw new ConflictException("All agreement need to get signed");
            }
            if(result.CompanyRoleIds.Contains(data.CompanyRole))
            {
                throw new ConflictException("companyRole already exists");   
            }
            _portalRepositories.GetInstance<ICompanyRolesRepository>().CreateCompanyAssignedRole(result.CompanyId, data.CompanyRole);

            var consentRepository = _portalRepositories.GetInstance<IConsentRepository>();
            foreach(var agreementData in data.Agreements)
            {
                if(result.ConsentStatusDetails.Any(csd => csd.AgreementId == agreementData.AgreementId))
                {
                    consentRepository.AttachAndModifiesConsents(result.ConsentStatusDetails.Select(csd => csd.ConsentId), consent =>
                        consent.ConsentStatusId = agreementData.ConsentStatus);
                }
                else
                {
                    consentRepository.CreateConsent(agreementData.AgreementId, result.CompanyId, result.CompanyUserId, agreementData.ConsentStatus);
                }
            }
        }
        await _portalRepositories.SaveAsync();
    }
}
