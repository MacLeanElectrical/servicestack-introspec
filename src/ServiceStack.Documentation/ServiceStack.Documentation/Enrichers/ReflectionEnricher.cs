﻿// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this 
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

namespace ServiceStack.Documentation.Enrichers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Reflection;
    using Extensions;
    using Host;
    using Interfaces;
    using Logging;
    using Models;
    using Settings;
    using Utilities;

    /// <summary>
    /// Enricher that will use Reflection to enrich object. 
    /// </summary>
    /// <remarks>This primarily looks at Attributes as well as types etc</remarks>
    public class ReflectionEnricher : IResourceEnricher, IRequestEnricher, IPropertyEnricher, ISecurityEnricher
    {
        private readonly ILog log = LogManager.GetLogger(typeof(ReflectionEnricher));

        private readonly Dictionary<string, ApiMemberAttribute> apiMemberLookup;

        public ReflectionEnricher()
        {
            apiMemberLookup = new Dictionary<string, ApiMemberAttribute>();
        }

        public string GetTitle(Type type) => type.Name;

        // [Api] then [ComponentModel.Description] then [DataAnnotations.Description]
        public string GetDescription(Type type) => type.GetDescription();

        public string GetNotes(Type type) => type.FirstAttribute<RouteAttribute>()?.Notes;

        public string[] GetVerbs(Operation operation)
        {
            return operation.Actions.Contains("ANY")
                ? DocumenterSettings.ReplacementVerbs as string[] ?? DocumenterSettings.ReplacementVerbs.ToArray()
                : operation.Actions.ToArray();
        }

        public string[] GetContentTypes(Operation operation)
        {
            // Get a list of all available formats
            var availableFormats = HostContext.MetadataPagesConfig.AvailableFormatConfigs.Select(a => a.Format);

            // NOTE Restriction can come from either DTO or Service
            RestrictAttribute restrictedTo = operation.RestrictTo;
            var requestType = operation.RequestType;

            var mimeTypes = new List<string>();
            foreach (var format in availableFormats.Select(s => s.TrimStart("x-")))
            {
                // Verify RestrictAttribute not preventing access
                var requestAttrResult = EnumUtilities.SafeParse<RequestAttributes>(format);
                if (!restrictedTo.CanAccess(requestAttrResult)) continue;

                // Verify ExcludeAttribute not preventing access
                var featureResult = EnumUtilities.SafeParse<Feature>(format);
                if (requestType.HasAccessToFeature(featureResult))
                    mimeTypes.Add(MimeTypeUtilities.GetMimeType(format));
            }

            ProcessAddHeaderAttribute(requestType, mimeTypes);

            return mimeTypes.Distinct().ToArray();
        }

        public StatusCode[] GetStatusCodes(Operation operation)
        {
            // From [ApiResponse] attributes
            var apiResponseAttributes = operation.RequestType.GetCustomAttributes<ApiResponseAttribute>();
            var responseAttributes = apiResponseAttributes as ApiResponseAttribute[] ?? apiResponseAttributes.ToArray();

            var list = new List<StatusCode>(responseAttributes.Length + 1);
            if (HasOneWayMethod(operation))
                list.Add((StatusCode) HttpStatusCode.NoContent);

            if (responseAttributes.Length == 0) return list.ToArray();

            foreach (var apiResponseAttribute in responseAttributes)
            {
                var statusCode = (StatusCode) apiResponseAttribute.StatusCode;
                statusCode.Description = apiResponseAttribute.Description;
                list.Add(statusCode);
            }

            return list.ToArray();
        }

        public string GetRelativePath(Operation operation)
        {
            // TODO Handle multiple routes for same DTO. Anything accessing [Route] will be affected
            var requestType = operation.RequestType;

            var routeFromAttribute = requestType.FirstAttribute<RouteAttribute>()?.Path;
            if (!string.IsNullOrWhiteSpace(routeFromAttribute))
                return routeFromAttribute;

            var emptyType = requestType.CreateInstance();

            // If no route then make one up
            return operation.IsOneWay ? emptyType.ToOneWayUrlOnly() : emptyType.ToReplyUrlOnly();
        }

        public string GetCategory(Operation operation) => null;
        public string[] GetTags(Operation operation) => null;

        public string GetTitle(MemberInfo mi) => GetApiMemberAttribute(mi)?.Name;
        public string GetDescription(MemberInfo mi) => GetApiMemberAttribute(mi)?.Description;
        public string GetParamType(MemberInfo mi) => GetApiMemberAttribute(mi)?.ParameterType;
        public bool? GetAllowMultiple(MemberInfo mi) => GetApiMemberAttribute(mi)?.AllowMultiple;
        public bool? GetIsRequired(MemberInfo mi) => GetApiMemberAttribute(mi)?.IsRequired;

        public string GetNotes(MemberInfo mi) => null;
        public string[] GetExternalLinks(MemberInfo mi) => null;

        public PropertyConstraint GetConstraints(MemberInfo mi)
        {
            var allowableValues = mi.FirstAttribute<ApiAllowableValuesAttribute>();
            if (allowableValues == null)
                return null;

            var constraint = allowableValues.Type == "LIST"
                                 ? PropertyConstraint.ListConstraint(allowableValues.Name, allowableValues.Values)
                                 : PropertyConstraint.RangeConstraint(allowableValues.Name, allowableValues.Min,
                                     allowableValues.Max);

            log.Debug($"Created {allowableValues.Type} constraint for property {mi.Name}");
            return constraint;
        }

        public ApiSecurity GetSecurity(Operation operation)
        {
            if (!operation.RequiresAuthentication)
                return null;

            var apiSecurity = new ApiSecurity
            {
                IsProtected = true,
                Roles = Permissions.Create(operation.RequiredRoles, operation.RequiresAnyRole),
                Permissions = Permissions.Create(operation.RequiredPermissions, operation.RequiresAnyPermission)
            };

            return apiSecurity;
        }

        private static bool HasOneWayMethod(Operation operation)
        {
            if (operation.IsOneWay)
                return true;

            return operation.ServiceType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Any(m =>
                    m.ReturnType == typeof (void)
                    && m.GetParameters().Any(p => p.ParameterType == operation.RequestType));
        }

        private ApiMemberAttribute GetApiMemberAttribute(MemberInfo pi)
        {
            ApiMemberAttribute attr;
            var piName = GetPropertyInfoName(pi);

            if (!apiMemberLookup.TryGetValue(piName, out attr))
            {
                attr = pi.FirstAttribute<ApiMemberAttribute>();
                if (attr == null)
                    return null;

                apiMemberLookup.Add(piName, attr);
            }

            return attr;
        }

        private string GetPropertyInfoName(MemberInfo pi) => $"{pi.DeclaringType?.FullName}.{pi.Name}";

        private static void ProcessAddHeaderAttribute(Type requestType, List<string> mimeTypes)
        {
            var addHeader = requestType.FirstAttribute<AddHeaderAttribute>();
            if (addHeader == null)
                return;

            // If [AddHeader] present then add that content-type
            var contentType = addHeader.ContentType ?? addHeader.DefaultContentType;
            if (!string.IsNullOrEmpty(contentType))
                mimeTypes.Add(contentType);
        }
    }
}