using BalboaSpa.Protocol;
using NUnit.Framework;

namespace BalboaSpa.Protocol.Tests
{
    [TestFixture]
    public class ConnectionOptionsTests
    {
        [Test]
        public void DefaultPortIs4257()
        {
            var options = BalboaConnectionOptions.CreateDefault("192.0.2.20");
            Assert.That(options.Port, Is.EqualTo(4257));
        }

        [Test]
        public void HostIsTrimmed()
        {
            var options = new BalboaConnectionOptions("  spa.local  ", 4257);
            Assert.That(options.Host, Is.EqualTo("spa.local"));
        }
    }
}
