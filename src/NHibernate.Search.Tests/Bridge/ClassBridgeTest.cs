using System.Collections;
using Lucene.Net.Analysis;
using Lucene.Net.QueryParsers;
using NHibernate.Cfg;
using NUnit.Framework;

namespace NHibernate.Search.Tests.Bridge
{
    /// <summary>
    /// This tests that a field created by a user-supplied
    /// EquipmentType class has been created and is a translation
    /// from an identifier to a manufacturer name.
    /// </summary>
    [TestFixture]
    public class ClassBridgeTest : SearchTestCase
    {
        protected override void Configure(Configuration configuration)
        {
            base.Configure(configuration);
            configuration.SetProperty(Environment.AnalyzerClass, typeof(SimpleAnalyzer).AssemblyQualifiedName);
        }

        protected override IList Mappings
        {
            get { return new string[] {"Bridge.Department.hbm.xml", "Bridge.Departments.hbm.xml"}; }
        }

        private Department getDept1()
        {
            Department dept = new Department();

            dept.Branch = "Salt Lake City";
            dept.BranchHead = "Kent Lewin";
            dept.MaxEmployees = 100;
            dept.Network = "1A";
            return dept;
        }

        private Department getDept2()
        {
            Department dept = new Department();

            dept.Branch = "Layton";
            dept.BranchHead = "Terry Poperszky";
            dept.MaxEmployees = 20;
            dept.Network = "2B";

            return dept;
        }

        private Department getDept3()
        {
            Department dept = new Department();

            dept.Branch = "West Valley";
            dept.BranchHead = "Pat Kelley";
            dept.MaxEmployees = 15;
            dept.Network = "3C";

            return dept;
        }

        private Departments getDepts1()
        {
            Departments depts = new Departments();

            depts.Branch = "Salt Lake City";
            depts.BranchHead = "Kent Lewin";
            depts.MaxEmployees = 100;
            depts.Network = "1A";
            depts.Manufacturer = "C";

            return depts;
        }

        private Departments getDepts2()
        {
            Departments depts = new Departments();

            depts.Branch = "Layton";
            depts.BranchHead = "Terry Poperszky";
            depts.MaxEmployees = 20;
            depts.Network = "2B";
            depts.Manufacturer = "3";

            return depts;
        }

        private Departments getDepts3()
        {
            Departments depts = new Departments();

            depts.Branch = "West Valley";
            depts.BranchHead = "Pat Kelley";
            depts.MaxEmployees = 15;
            depts.Network = "3C";
            depts.Manufacturer = "D";

            return depts;
        }

        private Departments getDepts4()
        {
            Departments depts = new Departments();

            depts.Branch = "St. George";
            depts.BranchHead = "Spencer Stajskal";
            depts.MaxEmployees = 10;
            depts.Network = "1D";
            depts.Manufacturer = "C";
            return depts;
        }

        /// <summary>
        /// This test checks for two fields being concatentated by the user-supplied
        /// <see cref="CatFieldsClassBridge" /> class which is specified as the implementation class
        /// in the ClassBridge annotation of the <see cref="Department" /> class.
        /// </summary>
        [Test]
        public void ClassBridge()
        {
            ISession s = OpenSession();
            ITransaction tx = s.BeginTransaction();
            s.Save(getDept1());
            s.Save(getDept2());
            s.Save(getDept3());
            s.Flush();
            tx.Commit();

            tx = s.BeginTransaction();
            IFullTextSession session = Search.CreateFullTextSession(s);

            // The branchnetwork field is the concatenation of both
            // the branch field and the network field of the Department
            // class. This is in the Lucene document but not in the
            // Department entity itself.
            QueryParser parser = new QueryParser("branchnetwork", new SimpleAnalyzer());

            Lucene.Net.Search.Query query = parser.Parse("branchnetwork:layton 2B");
            IFullTextQuery hibQuery = session.CreateFullTextQuery(query);
            IList result = hibQuery.List();
            Assert.IsNotNull(result);
            Assert.AreEqual("2B", ((Department) result[0]).Network, "incorrect entity returned, wrong network");
            Assert.AreEqual("Layton", ((Department) result[0]).Branch, "incorrect entity returned, wrong branch");
            Assert.AreEqual(1, result.Count, "incorrect number of results returned");

            // Partial match.
            query = parser.Parse("branchnetwork:3c");
            hibQuery = session.CreateFullTextQuery(query);
            result = hibQuery.List();
            Assert.IsNotNull(result);
            Assert.AreEqual("3C", ((Department) result[0]).Network, "incorrect entity returned, wrong network");
            Assert.AreEqual("West Valley", ((Department) result[0]).Branch, "incorrect entity returned, wrong branch");
            Assert.AreEqual(1, result.Count, "incorrect number of results returned");

            // No data cross-ups .
            query = parser.Parse("branchnetwork:Kent Lewin");
            hibQuery = session.CreateFullTextQuery(query);
            result = hibQuery.List();
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Count == 0, "problem with field cross-ups");

            // Non-ClassBridge field.
            parser = new QueryParser("BranchHead", new SimpleAnalyzer());
            query = parser.Parse("BranchHead:Kent Lewin");
            hibQuery = session.CreateFullTextQuery(query);
            result = hibQuery.List();
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Count == 1, "incorrect entity returned, wrong branch head");
            Assert.AreEqual("Kent Lewin", ((Department) result[0]).BranchHead, "incorrect entity returned");

            // Cleanup
            foreach (object element in s.CreateQuery("from " + typeof(Department).FullName).List())
                s.Delete(element);
            tx.Commit();
            s.Close();
        }

        [Test]
        public void ClassBridges()
        {
            ISession s = OpenSession();
            ITransaction tx = s.BeginTransaction();
            s.Save(getDepts1());
            s.Save(getDepts2());
            s.Save(getDepts3());
            s.Save(getDepts4());
            s.Flush();
            tx.Commit();

            tx = s.BeginTransaction();
            IFullTextSession session = Search.CreateFullTextSession(s);

            // The equipment field is the manufacturer field  in the
            // Departments entity after being massaged by passing it
            // through the EquipmentType class. This field is in
            // the Lucene document but not in the Department entity itself.
            QueryParser parser = new QueryParser("equipment", new SimpleAnalyzer());

            // Check the second ClassBridge annotation
            Lucene.Net.Search.Query query = parser.Parse("equiptype:Cisco");
            IFullTextQuery hibQuery = session.CreateFullTextQuery(query);
            IList result = hibQuery.List();
            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.Count, "incorrect number of results returned");
            foreach (Departments d in result)
            {
                Assert.AreEqual("C", d.Manufacturer, "incorrect manufacturer");
            }

            // No data cross-ups.
            query = parser.Parse("branchnetwork:Kent Lewin");
            hibQuery = session.CreateFullTextQuery(query);
            result = hibQuery.List();
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Count == 0, "problem with field cross-ups");

            // Non-ClassBridge field.
            parser = new QueryParser("BranchHead", new SimpleAnalyzer());
            query = parser.Parse("BranchHead:Kent Lewin");
            hibQuery = session.CreateFullTextQuery(query);
            result = hibQuery.List();
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Count == 1, "incorrect entity returned, wrong branch head");
            Assert.AreEqual("Kent Lewin", ((Departments) result[0]).BranchHead, "incorrect entity returned");

            // Check other ClassBridge annotation.
            parser = new QueryParser("branchnetwork", new SimpleAnalyzer());
            query = parser.Parse("branchnetwork:st. george 1D");
            hibQuery = session.CreateFullTextQuery(query);
            result = hibQuery.List();
            Assert.IsNotNull(result);
            Assert.AreEqual("1D", ((Departments) result[0]).Network, "incorrect entity returned, wrong network");
            Assert.AreEqual("St. George", ((Departments) result[0]).Branch, "incorrect entity returned, wrong branch");
            Assert.AreEqual(1, result.Count, "incorrect number of results returned");

            // Cleanup
            foreach (object element in s.CreateQuery("from " + typeof(Departments).FullName).List())
                s.Delete(element);
            tx.Commit();
            s.Close();
        }

        [Test, Ignore("Projection functionality not implemented yet")]
        public void ClassBridgesWithProjection()
        {
        }
    }
}