using Azure.Messaging.ServiceBus;
using CalculateFunding.Tests.Common.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CalculateFunding.Tests.Common.Builders
{
    public class MessageBuilder : TestEntityBuilder
    {
        private readonly ICollection<(string key, string value)> _properties = new List<(string key, string value)>();
        private byte[] _messageBody;

        public MessageBuilder WithoutUserProperty(string key)
        {
            _properties.Remove(_properties.First(_ => _.key == key));

            return this;
        }


        public MessageBuilder WithMessageBody(byte[] messageBody)
        {
            _messageBody = messageBody;

            return this;
        }

        public MessageBuilder WithUserProperty(string key, string value)
        {
            _properties.Add((key, value));

            return this;
        }

        public ServiceBusReceivedMessage Build()
        {

            var properties = new Dictionary<string, object>();

            foreach ((string key, string value) property in _properties)
            {
                properties.Add(property.key, property.value);
            }

            ServiceBusReceivedMessage message = ServiceBusModelFactory.ServiceBusReceivedMessage(
              body: _messageBody != null ? new BinaryData(_messageBody) : new BinaryData(String.Empty),
              properties: properties,
              messageId: Guid.NewGuid().ToString()
              );

            return message;
        }
    }
}