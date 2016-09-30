﻿////////////////////////////////////////////////////////////////////////////
//
// Copyright 2016 Realm Inc.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
////////////////////////////////////////////////////////////////////////////

using NUnit.Framework;
using Realms;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace IntegrationTests.Shared
{
    [TestFixture, Preserve(AllMembers = true)]
    public class StandAloneObjectTests
    {
        private Person _person;

        [SetUp]
        public void SetUp()
        {
            _person = new Person();
            Realm.DeleteRealm(RealmConfiguration.DefaultConfiguration);
        }

        [Test]
        public void PropertyGet()
        {
            string firstName = null;
            Assert.DoesNotThrow(() => firstName = _person.FirstName);
            Assert.That(string.IsNullOrEmpty(firstName));
        }

        [Test]
        public void PropertySet()
        {
            const string name = "John";
            Assert.DoesNotThrow(() => _person.FirstName = name);
            Assert.AreEqual(name, _person.FirstName);
        }

        void SetStandalonePerson()
        {
            _person.FirstName = "Arthur";
            _person.LastName = "Dent";
            _person.IsInteresting = true;
        }


        [Test]
        public void AddToRealm()
        {
            SetStandalonePerson();
            using (var realm = Realm.GetInstance())
            {
                using (var transaction = realm.BeginWrite())
                {
                    realm.Manage(_person);
                    transaction.Commit();
                }

                Assert.That(_person.IsManaged);

                var p = realm.All<Person>().Single();
                Assert.That(p.FirstName, Is.EqualTo("Arthur"));
                Assert.That(p.LastName, Is.EqualTo("Dent"));
                Assert.That(p.IsInteresting);
            }
        }

        [Test]
        public void RealmObject_WhenStandalone_ShouldHaveDefaultEqualsImplementation()
        {
            var otherPerson = new Person();

            Assert.DoesNotThrow(() => _person.Equals(otherPerson));
        }


        [Test]
        public void CopySimpleToRealmAdds()
        {
            SetStandalonePerson();
            using (var realm = Realm.GetInstance())
            {
                var numBefore = realm.All<Person>().Count();
                realm.Write(() =>
                {
                    var newlyManaged = realm.CopyToRealmOrUpdate(_person);
                    Assert.That(newlyManaged.IsManaged);
                    Assert.IsFalse(_person.IsManaged);  // left untouched
                    Assert.That(newlyManaged.FullName, Is.EqualTo(_person.FullName));
                });
                Assert.That(numBefore, Is.EqualTo(realm.All<Person>().Count() - 1));
            }
        }

    }
}
