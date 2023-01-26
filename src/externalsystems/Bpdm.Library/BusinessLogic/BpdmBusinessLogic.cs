/********************************************************************************
 * Copyright (c) 2021,2022 Microsoft and BMW Group AG
 * Copyright (c) 2021,2022 Contributors to the Eclipse Foundation
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

using Org.Eclipse.TractusX.Portal.Backend.Bpdm.Library.Models;
using Org.Eclipse.TractusX.Portal.Backend.Framework.ErrorHandling;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Repositories;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Enums;

namespace Org.Eclipse.TractusX.Portal.Backend.Bpdm.Library.BusinessLogic;

public class BpdmBusinessLogic : IBpdmBusinessLogic
{
    private readonly IPortalRepositories _portalRepositories;
    private readonly IBpdmService _bpdmService;

    public BpdmBusinessLogic(IPortalRepositories portalRepositories, IBpdmService bpdmService)
    {
        _portalRepositories = portalRepositories;
        _bpdmService = bpdmService;
    }

    public async Task<bool> PushLegalEntity(Guid applicationId, string iamUserId, CancellationToken cancellationToken)
    {
        var (isValidApplicationId, data, isUserInCompany) = await _portalRepositories.GetInstance<IApplicationRepository>().GetBpdmDataForApplicationAsync(iamUserId, applicationId).ConfigureAwait(false);
        if (!isValidApplicationId)
        {
            throw new NotFoundException($"Application {applicationId} does not exists.");
        }

        if (!isUserInCompany)
        {
            throw new ForbiddenException($"User is not allowed to trigger Bpn Data Push for the application {applicationId}");
        }

        if (data == null)
        {
            throw new UnexpectedConditionException($"BpdmData should never be null here");
        }

        if (data.ApplicationStatusId != CompanyApplicationStatusId.SUBMITTED)
        {
            throw new ConflictException($"CompanyApplication {applicationId} is not in status SUBMITTED");
        }

        if (!string.IsNullOrWhiteSpace(data.BusinessPartnerNumber))
        {
            throw new ConflictException($"BusinessPartnerNumber is already set");
        }

        if (string.IsNullOrWhiteSpace(data.Alpha2Code))
        {
            throw new ConflictException("Alpha2Code must not be empty");
        }

        if (string.IsNullOrWhiteSpace(data.City))
        {
            throw new ConflictException("City must not be empty");
        }

        if (string.IsNullOrWhiteSpace(data.StreetName))
        {
            throw new ConflictException("StreetName must not be empty");
        }

        var bpdmTransferData = new BpdmTransferData(
            data.CompanyId,
            data.CompanyName,
            data.ShortName,
            data.Alpha2Code,
            data.ZipCode,
            data.City,
            data.StreetName,
            data.StreetNumber,
            data.Region,
            data.Identifiers);
        return await _bpdmService.PutInputLegalEntity(bpdmTransferData, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> PullLegalEntity(Guid applicationId, CancellationToken cancellationToken)
    {
        var data = await _portalRepositories.GetInstance<IApplicationRepository>().GetBpdmDataForApplicationAsync(applicationId).ConfigureAwait(false);
        
        if (data == null)
        {
            throw new ConflictException($"CompanyApplication {applicationId} does not exist");
        }

        if (data.ApplicationStatusId != CompanyApplicationStatusId.SUBMITTED)
        {
            throw new ConflictException($"CompanyApplication {applicationId} is not in status SUBMITTED");
        }

        var legalEntity = await _bpdmService.FetchInputLegalEntity(data.CompanyId, cancellationToken).ConfigureAwait(false);

        if (legalEntity == null)
        {
            throw new ConflictException($"legal-entity not found in bpdm for companyId {data.CompanyId}");
        }

        if (string.IsNullOrEmpty(legalEntity.Bpn))
        {
            return false;
        }

        // TODO: either throw error if address- or identifier-data returned by bpdm does not match what is stored in portal-db or modify in portal-db based on bpdm-response

        _portalRepositories.GetInstance<ICompanyRepository>().AttachAndModifyCompany(
            data.CompanyId,
            company => 
            {
                company.BusinessPartnerNumber = data.BusinessPartnerNumber;
            },
            company =>
            {
                company.BusinessPartnerNumber = legalEntity.Bpn;
            });

        return true;
    }
}
