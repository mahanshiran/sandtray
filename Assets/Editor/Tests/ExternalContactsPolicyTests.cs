using System.Reflection;
using NUnit.Framework;
using Sandplay.Core;

namespace Sandplay.Tests
{
    public class ExternalContactsPolicyTests
    {
        private BackendClient _backend;
        private string _originalType;
        private bool _originalManaged;
        private bool _originalExternalContacts;

        [SetUp]
        public void SetUp()
        {
            _backend = BackendClient.Instance;
            _originalType = _backend.UserType;
            _originalManaged = _backend.IsManagedTherapist;
            _originalExternalContacts = _backend.ManagedAllowExternalContacts;
        }

        [TearDown]
        public void TearDown()
        {
            Set("UserType", _originalType);
            Set("IsManagedTherapist", _originalManaged);
            Set("ManagedAllowExternalContacts", _originalExternalContacts);
        }

        [Test]
        public void IndependentTherapistKeepsFriendsEvenWithOrganizationMembership()
        {
            Set("UserType", "psychologist");
            Set("IsManagedTherapist", true);
            Set("ManagedAllowExternalContacts", false);

            Assert.IsTrue(_backend.ExternalContactsAllowed);
        }

        [TestCase(false, false)]
        [TestCase(true, true)]
        public void OrganizationCreatedTherapistUsesOrganizationPermission(bool permission, bool expected)
        {
            Set("UserType", "therapist_org");
            Set("IsManagedTherapist", true);
            Set("ManagedAllowExternalContacts", permission);

            Assert.AreEqual(expected, _backend.ExternalContactsAllowed);
        }

        private void Set(string property, object value) =>
            typeof(BackendClient).GetProperty(property, BindingFlags.Instance | BindingFlags.Public)
                .SetValue(_backend, value);
    }
}
