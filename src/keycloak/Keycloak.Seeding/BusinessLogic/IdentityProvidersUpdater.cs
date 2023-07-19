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

using Org.Eclipse.TractusX.Portal.Backend.Framework.ErrorHandling;
using Org.Eclipse.TractusX.Portal.Backend.Framework.Linq;
using Org.Eclipse.TractusX.Portal.Backend.Keycloak.ErrorHandling;
using Org.Eclipse.TractusX.Portal.Backend.Keycloak.Factory;
using Org.Eclipse.TractusX.Portal.Backend.Keycloak.Library.Models.IdentityProviders;
using Org.Eclipse.TractusX.Portal.Backend.Keycloak.Seeding.Models;

namespace Org.Eclipse.TractusX.Portal.Backend.Keycloak.Seeding.BusinessLogic;

public class IdentityProvidersUpdater : IIdentityProvidersUpdater
{
    private readonly IKeycloakFactory _keycloakFactory;
    private readonly ISeedDataHandler _seedData;

    public IdentityProvidersUpdater(IKeycloakFactory keycloakFactory, ISeedDataHandler seedDataHandler)
    {
        _keycloakFactory = keycloakFactory;
        _seedData = seedDataHandler;
    }

    public async Task UpdateIdentityProviders(string keycloakInstanceName)
    {
        var keycloak = _keycloakFactory.CreateKeycloakClient(keycloakInstanceName);
        var realm = _seedData.Realm;

        foreach (var updateIdentityProvider in _seedData.IdentityProviders)
        {
            if (updateIdentityProvider.Alias == null)
                throw new ConflictException($"identityProvider alias must not be null: {updateIdentityProvider.InternalId} {updateIdentityProvider.DisplayName}");

            try
            {
                var identityProvider = await keycloak.GetIdentityProviderAsync(realm, updateIdentityProvider.Alias).ConfigureAwait(false);
                if (!Compare(identityProvider, updateIdentityProvider))
                {
                    UpdateIdentityProvider(identityProvider, updateIdentityProvider);
                    await keycloak.UpdateIdentityProviderAsync(realm, updateIdentityProvider.Alias, identityProvider).ConfigureAwait(false);
                }
            }
            catch (KeycloakEntityNotFoundException)
            {
                var identityProvider = new IdentityProvider();
                UpdateIdentityProvider(identityProvider, updateIdentityProvider);
                await keycloak.CreateIdentityProviderAsync(realm, identityProvider).ConfigureAwait(false);
            }

            var updateMappers = _seedData.IdentityProviderMappers.Where(x => x.IdentityProviderAlias == updateIdentityProvider.Alias);
            var mappers = await keycloak.GetIdentityProviderMappersAsync(realm, updateIdentityProvider.Alias).ConfigureAwait(false);

            // create missing mappers
            foreach (var mapper in updateMappers.ExceptBy(mappers.Select(x => x.Name), x => x.Name))
            {
                await keycloak.AddIdentityProviderMapperAsync(
                    realm,
                    updateIdentityProvider.Alias,
                    UpdateIdentityProviderMapper(
                        new IdentityProviderMapper
                        {
                            Name = mapper.Name,
                            IdentityProviderAlias = mapper.IdentityProviderAlias
                        },
                        mapper)).ConfigureAwait(false);
            }

            // update existing mappers
            foreach (var x in mappers.Join(
                updateMappers,
                x => x.Name,
                x => x.Name,
                (mapper, update) => (Mapper: mapper, Update: update)))
            {
                var (mapper, update) = x;
                if (!Compare(mapper, update))
                {
                    await keycloak.UpdateIdentityProviderMapperAsync(
                        realm,
                        updateIdentityProvider.Alias,
                        mapper.Id ?? throw new ConflictException($"identityProviderMapper.id must never be null {mapper.Name} {mapper.IdentityProviderAlias}"),
                        UpdateIdentityProviderMapper(mapper, update)).ConfigureAwait(false);
                }
            }

            // delete redundant mappers
            foreach (var mapper in mappers.ExceptBy(updateMappers.Select(x => x.Name), x => x.Name))
            {
                await keycloak.DeleteIdentityProviderMapperAsync(
                    realm,
                    updateIdentityProvider.Alias,
                    mapper.Id ?? throw new ConflictException($"identityProviderMapper.id must never be null {mapper.Name} {mapper.IdentityProviderAlias}")).ConfigureAwait(false);
            }
        }
    }

    private static IdentityProvider UpdateIdentityProvider(IdentityProvider provider, IdentityProviderModel update)
    {
        provider.Alias = update.Alias;
        provider.DisplayName = update.DisplayName;
        provider.ProviderId = update.ProviderId;
        provider.Enabled = update.Enabled;
        provider.UpdateProfileFirstLoginMode = update.UpdateProfileFirstLoginMode;
        provider.TrustEmail = update.TrustEmail;
        provider.StoreToken = update.StoreToken;
        provider.AddReadTokenRoleOnCreate = update.AddReadTokenRoleOnCreate;
        provider.AuthenticateByDefault = update.AuthenticateByDefault;
        provider.LinkOnly = update.LinkOnly;
        provider.FirstBrokerLoginFlowAlias = update.FirstBrokerLoginFlowAlias;
        provider.Config = update.Config == null
            ? null
            : new Config
            {
                HideOnLoginPage = update.Config.HideOnLoginPage,
                //ClientSecret = update.Config.ClientSecret,
                DisableUserInfo = update.Config.DisableUserInfo,
                ValidateSignature = update.Config.ValidateSignature,
                ClientId = update.Config.ClientId,
                TokenUrl = update.Config.TokenUrl,
                AuthorizationUrl = update.Config.AuthorizationUrl,
                ClientAuthMethod = update.Config.ClientAuthMethod,
                JwksUrl = update.Config.JwksUrl,
                LogoutUrl = update.Config.LogoutUrl,
                ClientAssertionSigningAlg = update.Config.ClientAssertionSigningAlg,
                SyncMode = update.Config.SyncMode,
                UseJwksUrl = update.Config.UseJwksUrl,
                UserInfoUrl = update.Config.UserInfoUrl,
                Issuer = update.Config.Issuer,
                // for Saml:
                NameIDPolicyFormat = update.Config.NameIDPolicyFormat,
                PrincipalType = update.Config.PrincipalType,
                SignatureAlgorithm = update.Config.SignatureAlgorithm,
                XmlSigKeyInfoKeyNameTransformer = update.Config.XmlSigKeyInfoKeyNameTransformer,
                AllowCreate = update.Config.AllowCreate,
                EntityId = update.Config.EntityId,
                AuthnContextComparisonType = update.Config.AuthnContextComparisonType,
                BackchannelSupported = update.Config.BackchannelSupported,
                PostBindingResponse = update.Config.PostBindingResponse,
                PostBindingAuthnRequest = update.Config.PostBindingAuthnRequest,
                PostBindingLogout = update.Config.PostBindingLogout,
                WantAuthnRequestsSigned = update.Config.WantAuthnRequestsSigned,
                WantAssertionsSigned = update.Config.WantAssertionsSigned,
                WantAssertionsEncrypted = update.Config.WantAssertionsEncrypted,
                ForceAuthn = update.Config.ForceAuthn,
                SignSpMetadata = update.Config.SignSpMetadata,
                LoginHint = update.Config.LoginHint,
                SingleSignOnServiceUrl = update.Config.SingleSignOnServiceUrl,
                AllowedClockSkew = update.Config.AllowedClockSkew,
                AttributeConsumingServiceIndex = update.Config.AttributeConsumingServiceIndex
            };
        return provider;
    }

    private static bool Compare(IdentityProvider provider, IdentityProviderModel update) =>
        provider.Alias == update.Alias &&
        provider.DisplayName == update.DisplayName &&
        provider.ProviderId == update.ProviderId &&
        provider.Enabled == update.Enabled &&
        provider.UpdateProfileFirstLoginMode == update.UpdateProfileFirstLoginMode &&
        provider.TrustEmail == update.TrustEmail &&
        provider.StoreToken == update.StoreToken &&
        provider.AddReadTokenRoleOnCreate == update.AddReadTokenRoleOnCreate &&
        provider.AuthenticateByDefault == update.AuthenticateByDefault &&
        provider.LinkOnly == update.LinkOnly &&
        provider.FirstBrokerLoginFlowAlias == update.FirstBrokerLoginFlowAlias &&
        Compare(provider.Config, update.Config);

    private static bool Compare(Config? config, IdentityProviderConfigModel? update) =>
        config == null && update == null ||
        config != null && update != null &&
        config.HideOnLoginPage == update.HideOnLoginPage &&
        //ClientSecret = update.ClientSecret &&
        config.DisableUserInfo == update.DisableUserInfo &&
        config.ValidateSignature == update.ValidateSignature &&
        config.ClientId == update.ClientId &&
        config.TokenUrl == update.TokenUrl &&
        config.AuthorizationUrl == update.AuthorizationUrl &&
        config.ClientAuthMethod == update.ClientAuthMethod &&
        config.JwksUrl == update.JwksUrl &&
        config.LogoutUrl == update.LogoutUrl &&
        config.ClientAssertionSigningAlg == update.ClientAssertionSigningAlg &&
        config.SyncMode == update.SyncMode &&
        config.UseJwksUrl == update.UseJwksUrl &&
        config.UserInfoUrl == update.UserInfoUrl &&
        config.Issuer == update.Issuer &&
        // for Saml:
        config.NameIDPolicyFormat == update.NameIDPolicyFormat &&
        config.PrincipalType == update.PrincipalType &&
        config.SignatureAlgorithm == update.SignatureAlgorithm &&
        config.XmlSigKeyInfoKeyNameTransformer == update.XmlSigKeyInfoKeyNameTransformer &&
        config.AllowCreate == update.AllowCreate &&
        config.EntityId == update.EntityId &&
        config.AuthnContextComparisonType == update.AuthnContextComparisonType &&
        config.BackchannelSupported == update.BackchannelSupported &&
        config.PostBindingResponse == update.PostBindingResponse &&
        config.PostBindingAuthnRequest == update.PostBindingAuthnRequest &&
        config.PostBindingLogout == update.PostBindingLogout &&
        config.WantAuthnRequestsSigned == update.WantAuthnRequestsSigned &&
        config.WantAssertionsSigned == update.WantAssertionsSigned &&
        config.WantAssertionsEncrypted == update.WantAssertionsEncrypted &&
        config.ForceAuthn == update.ForceAuthn &&
        config.SignSpMetadata == update.SignSpMetadata &&
        config.LoginHint == update.LoginHint &&
        config.SingleSignOnServiceUrl == update.SingleSignOnServiceUrl &&
        config.AllowedClockSkew == update.AllowedClockSkew &&
        config.AttributeConsumingServiceIndex == update.AttributeConsumingServiceIndex;

    private static IdentityProviderMapper UpdateIdentityProviderMapper(IdentityProviderMapper mapper, IdentityProviderMapperModel updateMapper)
    {
        mapper._IdentityProviderMapper = updateMapper.IdentityProviderMapper;
        mapper.Config = updateMapper.Config?.ToDictionary(x => x.Key, x => x.Value);
        return mapper;
    }

    private static bool Compare(IdentityProviderMapper mapper, IdentityProviderMapperModel updateMapper) =>
        mapper.IdentityProviderAlias == updateMapper.IdentityProviderAlias &&
        mapper._IdentityProviderMapper == updateMapper.IdentityProviderMapper &&
        mapper.Config.NullOrContentEqual(updateMapper.Config);
}