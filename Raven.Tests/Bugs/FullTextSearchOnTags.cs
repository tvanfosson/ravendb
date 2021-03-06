using System.Collections.Generic;
using Raven.Abstractions.Indexing;
using Xunit;
using Raven.Client.Linq;
using System.Linq;

namespace Raven.Tests.Bugs
{
	public class FullTextSearchOnTags : RavenTest
	{
		public class Image
		{
			public string Id { get; set; }
			public string Name { get; set; }
			public ICollection<string> Users { get; set; }
			public ICollection<string> Tags { get; set; }
		}

		[Fact]
		public void CanSearchUsingPhrase()
		{
			using (var store = NewDocumentStore())
			{
				using (var session = store.OpenSession())
				{
					session.Store(new Image
					{
						Tags = new[] { "cats", "animal", "feline" }
					});
					session.SaveChanges();
				}

				store.DatabaseCommands.PutIndex("test", new IndexDefinition
				{
					Map = "from doc in docs.Images select new { doc.Tags }",
				});

				using (var session = store.OpenSession())
				{
					var images = session.Query<Image>("test")
						.Customize(x => x.WaitForNonStaleResults())
						.Search(x => x.Tags, "i love cats")
						.ToList();
					Assert.NotEmpty(images);
				}
			}
		}

		[Fact]
		public void CanSearchUsingPhraseAndOrderBy()
		{
			using (var store = NewDocumentStore())
			{
				using (var session = store.OpenSession())
				{
					session.Store(new Image
					{
						Name = "B",
						Tags = new[] { "cats", "animal", "feline" }
					});
					session.Store(new Image
					{
						Name = "A",
						Tags = new[] { "cats", "animal", "feline" }
					});
					session.SaveChanges();
				}

				store.DatabaseCommands.PutIndex("test", new IndexDefinition
				{
					Map = "from doc in docs.Images select new { doc.Tags,doc.Name }",
				});

				using (var session = store.OpenSession())
				{
					var images = session.Query<Image>("test")
						.Customize(x => x.WaitForNonStaleResults())
						.OrderBy(x => x.Name)
						.Search(x => x.Tags, "i love cats")
						.ToList();
					Assert.NotEmpty(images);

					Assert.Equal("images/2", images[0].Id);
					Assert.Equal("images/1", images[1].Id);
				}
			}
		}

		[Fact]
		public void CanSearchUsingPhrase_MultipleSearches()
		{
			using (var store = NewDocumentStore())
			{
				using (var session = store.OpenSession())
				{
					session.Store(new Image
					{
						Tags = new[] { "cats", "animal", "feline" }
					});

					session.Store(new Image
					{
						Tags = new[] { "dogs", "animal", "canine" }
					});
					session.SaveChanges();
				}

				store.DatabaseCommands.PutIndex("test", new IndexDefinition
				{
					Map = "from doc in docs.Images select new { doc.Tags }",
				});

				using (var session = store.OpenSession())
				{
					var images = session.Query<Image>("test")
						.Customize(x => x.WaitForNonStaleResults())
						.Search(x => x.Tags, "i love cats")
						.Search(x => x.Tags, "canine love")
						.ToList();
					Assert.Equal(2, images.Count);
				}
			}
		}

		[Fact]
		public void BoostingSearches()
		{
			using (var store = NewDocumentStore())
			{

				store.DatabaseCommands.PutIndex("test", new IndexDefinition
				{
					Map = "from doc in docs.Images select new { doc.Tags }",
				});

				using (var session = store.OpenSession())
				{
					session.Store(new Image
					{
						Tags = new[] { "cats", "animal", "feline" }
					});

					session.Store(new Image
					{
						Tags = new[] { "dogs", "animal", "canine" }
					});
					session.SaveChanges();
				}

				using (var session = store.OpenSession())
				{
					var ravenQueryable = session.Query<Image>("test")
						.Customize(x => x.WaitForNonStaleResults())
						.Search(x => x.Tags, "i love cats", boost: 3)
						.Search(x => x.Tags, "canine love", boost: 13);
					var s = ravenQueryable
						.ToString();
					Assert.Equal("Tags:<<i love cats>>^3 Tags:<<canine love>>^13", s);

					var images = ravenQueryable.ToList();

					Assert.Equal(2, images.Count);
					Assert.Equal("images/2", images[0].Id);
					Assert.Equal("images/1", images[1].Id);
				}
			}
		}

		[Fact]
		public void MultipleSearches()
		{
			using (var store = NewDocumentStore())
			{
				using (var session = store.OpenSession())
				{
					session.Store(new Image
					{
						Tags = new[] { "cats", "animal", "feline" },
						Users = new[] { "oren", "ayende" }
					});
					session.SaveChanges();
				}

				store.DatabaseCommands.PutIndex("test", new IndexDefinition
				{
					Map = "from doc in docs.Images select new { doc.Tags, doc.Users }",
				});

				using (var session = store.OpenSession())
				{
					var query = session.Query<Image>("test")
						.Customize(x => x.WaitForNonStaleResults())
						.Search(x => x.Tags, "i love cats")
						.Search(x => x.Users, "oren")
						.ToString();
					Assert.Equal("Tags:<<i love cats>> Users:<<oren>>", query.Trim());
				}
			}
		}

		[Fact]
		public void UsingSuggest()
		{
			using (var store = NewDocumentStore())
			{
				using (var session = store.OpenSession())
				{
					session.Store(new Image
					{
						Tags = new[] { "cats", "animal", "feline" },
						Users = new[] { "oren", "ayende" }
					});
					session.SaveChanges();
				}

				store.DatabaseCommands.PutIndex("test", new IndexDefinition
				{
					Map = "from doc in docs.Images select new { doc.Tags, doc.Users }",
					Indexes = { { "Tags", FieldIndexing.Analyzed } }
				});

				using (var session = store.OpenSession())
				{
					session.Query<Image>("test")
						.Customize(x => x.WaitForNonStaleResults())
						.ToList();

					var query = session.Query<Image>("test")
						.Search(x => x.Tags, "anmal lover")
						.Suggest();
					Assert.NotEmpty(query.Suggestions);
					Assert.Equal("animal", query.Suggestions[0]);
				}
			}
		}

		[Fact]
		public void Can_search_inner_words()
		{
			using (var store = NewDocumentStore())
			{
				using (var session = store.OpenSession())
				{
					session.Store(new Image { Id = "1", Name = "Great Photo buddy" });
					session.Store(new Image { Id = "2", Name = "Nice Photo of the sky" });
					session.SaveChanges();
				}

				store.DatabaseCommands.PutIndex("test", new IndexDefinition
				{
					Map = "from doc in docs.Images select new { doc.Name }",
					Indexes = {
					    { "Name", FieldIndexing.Analyzed }
				    }

				});

				using (var session = store.OpenSession())
				{
					var images = session.Query<Image>("test")
						.Customize(x => x.WaitForNonStaleResults())
						.OrderBy(x => x.Name)
						.Search(x => x.Name, "Photo")
						.ToList();
					Assert.NotEmpty(images);
				}
			}
		}

	}
}