﻿// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this 
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

namespace ServiceStack.IntroSpec.Tests.Enrichers.Infrastructure
{
    using System;
    using FakeItEasy;
    using FluentAssertions;
    using Host;
    using IntroSpec.Enrichers.Infrastructure;
    using IntroSpec.Enrichers.Interfaces;
    using IntroSpec.Models;
    using Xunit;

    public class ResourceEnricherManagerTests
    {
        private readonly ResourceEnricherManager nullParameterManager;
        private readonly ResourceEnricherManager manager;
        private readonly IResourceEnricher resourceEnricher;
        private readonly IPropertyEnricher propertyEnricher;

        public ResourceEnricherManagerTests()
        {
            nullParameterManager = new ResourceEnricherManager(null, null);

            resourceEnricher = A.Fake<IResourceEnricher>();
            manager = new ResourceEnricherManager(resourceEnricher, null);
        }

        [Fact]
        public void Ctor_AllowsNullResourceEnricher()
        {
            Action action = () => new ResourceEnricherManager(null, propertyEnricher);
            action.Should().NotThrow<ArgumentNullException>();
        }

        [Fact]
        public void Ctor_AllowsNullParameterEnricher()
        {
            Action action = () => new ResourceEnricherManager(resourceEnricher, null);
            action.Should().NotThrow<ArgumentNullException>();
        }

        [Fact]
        public void EnrichResource_HandlesNullResourceEnricher()
        {
            nullParameterManager.EnrichResource(new ApiResourceDocumentation(), new Operation());
            // no assert but no error
        }

        [Fact]
        public void EnrichResource_CallsGetTitle_IfResourceHasNoTitle()
        {
            var responseType = typeof(int);
            manager.EnrichResource(new ApiResourceType { TypeName = "string" },
                new Operation { ResponseType = responseType });
            A.CallTo(() => resourceEnricher.GetTitle(responseType)).MustHaveHappened();
        }

        [Fact]
        public void EnrichResource_DoesNotCallGetTitle_IfResourceHasTitle()
        {
            manager.EnrichResource(new ApiResourceType { Title = "mo" }, new Operation());
            A.CallTo(() => resourceEnricher.GetTitle(A<Type>.Ignored)).MustNotHaveHappened();
        }

        [Fact]
        public void EnrichResource_CallsGetDescription_IfResourceHasNoDescription()
        {
            var responseType = typeof(int);
            manager.EnrichResource(new ApiResourceType(), new Operation { ResponseType = responseType });
            A.CallTo(() => resourceEnricher.GetDescription(responseType)).MustHaveHappened();
        }

        [Fact]
        public void EnrichResource_DoesNotCallGetDescription_IfResourceHasDescription()
        {
            manager.EnrichResource(new ApiResourceType { Description = "desk" }, new Operation());
            A.CallTo(() => resourceEnricher.GetDescription(A<Type>.Ignored)).MustNotHaveHappened();
        }
        
        [Fact]
        public void EnrichResource_CallsGetNotes_IfResourceHasNoNotes()
        {
            var responseType = typeof(int);
            manager.EnrichResource(new ApiResourceType(), new Operation { ResponseType = responseType });
            A.CallTo(() => resourceEnricher.GetNotes(responseType)).MustHaveHappened();
        }

        [Fact]
        public void EnrichResource_DoesNotCallGetNotes_IfResourceHasNotes()
        {
            manager.EnrichResource(new ApiResourceType { Notes = "mo" }, new Operation());
            A.CallTo(() => resourceEnricher.GetNotes(A<Type>.Ignored)).MustNotHaveHappened();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void EnrichResource_DoesNotCallGetAllowMultiple_IfAllowMultipleHasValue(bool val)
        {
            manager.EnrichResource(new ApiResourceType { AllowMultiple = val }, new Operation());
            A.CallTo(() => resourceEnricher.GetAllowMultiple(A<Type>.Ignored)).MustNotHaveHappened();
        }

        [Fact]
        public void EnrichResource_CallsGetAllowMultiple_IfAllowMultipleHasNoValue()
        {
            var reqType = new EnrichTest { AllowMultiple = null };
            manager.EnrichResource(reqType, new Operation { RequestType = reqType.GetType() });
            A.CallTo(() => resourceEnricher.GetAllowMultiple(reqType.GetType())).MustHaveHappened();
        }

        internal class EnrichTest : ApiResourceType, IApiRequest
        {
            public string Category { get; set; }

            public string[] Tags { get; set; }

            public ApiResourceType ReturnType { get; set; }

            public ApiAction[] Actions { get; set; }

            public bool? AllowMultiple { get; set; }

            public bool? HasValidator { get; set; }
        }
    }
}
