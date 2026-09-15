using Azure.Messaging.ServiceBus;
using CalculateFunding.Common.Models;
using CalculateFunding.Models.Messages;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;


namespace CalculateFunding.Services.Core.Extensions
{
    [TestClass]
    public class MessageExtensionTests
    {
        [TestMethod]
        public void GetMessageBodyStringFromMessage_GivenUnCompressedBody_ReturnsJson()
        {
            //Arrange
            SpecificationVersionComparisonModel specificationVersionComparison = new SpecificationVersionComparisonModel()
            {
                Id = "spec-1",
                Current = new Models.Messages.SpecificationVersion
                {
                    FundingPeriod = new Reference { Id = "fp1" },
                    Name = "any-name"
                },
                Previous = new Models.Messages.SpecificationVersion
                {
                    FundingPeriod = new Reference { Id = "fp1" }
                }
            };

            string json = JsonConvert.SerializeObject(specificationVersionComparison);


            // Build a *received* message for unit tests using the model factory
            var message = ServiceBusModelFactory.ServiceBusReceivedMessage(
                body: BinaryData.FromString(json),
                contentType: "application/json",
                
                properties: new Dictionary<string, object>()
            );

            // Act
            string result = MessageExtensions.GetMessageBodyStringFromMessage(message);

            // Assert
            result.Should().Be(json);
        }
    }
}
