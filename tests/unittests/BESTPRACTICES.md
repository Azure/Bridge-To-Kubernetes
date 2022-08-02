# Unit Test Best Practices

**Contents**
- [Unit Test Guidelines](#guidelines)<br>
- [FakeItEasy tips](#fakeTips)
- [Resources](#resources)<br>
- [See Also](#seeAlso)<br>

<a id="guidelines">**Unit Test Guidelines**</a><br>
Some guidelines to keep in mind while writing unit tests:
- 100% code coverage is not necessary, and probably not the best use of your time. Use your best judgement to decide which code and conditions are important to cover. 
- Prioritize breadth of coverage (more scenarios) over depth (a super detailed scenario).
- Since our product code uses Autofac dependency injection, it is intuitive that we use FakeItEasy for writing tests. Testing via FakeItEasy also helps ensure that our product code conforms to Autofac best practices (e.g. no <code>new</code> statements or statics). See [FakeItEasy tips](#fakeTips) below.
- While writing tests for a specific component, DO NOT assume that the behavior a method portrays is correct. It may be that you uncover bugs as you test the method. Make sure you understand the context in which the method is used and the behavior it "should" depict.
- If you see static methods in product code that are not simple helper functions, that's a red flag. Question why the method needs to be static, and consider converting it to an instance method on an interface so it can be faked.
- Refactoring is your best friend - if a component seems huge and untestable, refactor it! It will make your life much easier to write unit tests once you refactor and break it down into smaller, testable chunks. Remember, we are writing unit tests, not integration or end-to-end tests.
- In addition to checking that code is returning the right values, tests should also verify that expected messages being logged (via ILog) and displayed to the user.
- If you have several tests that are very similar or one test that cycles through a set of inputs and expected results, consider creating a single parameterized test using the [Theory] and [InlineData] attributes. See ConfigureJobManagerTest.StartPatchAsync_ValidateVariableValues() for an example.
- Inherit all test classes from IDisposable and don't forget to dispose the AutoFake instance (and any other disposable fields) in your test class!
- FakeItEasy can help in categorizing tests depending on how you want to run them - serially or parallelly or grouping them under a certain name etc. Don't hesitate to dig deeper into [the documentation](https://fakeiteasy.readthedocs.io/en/stable/) to find features that you're looking for!

<a id="fakeTips">**FakeItEasy tips**</a>
- To test product code, instantiate the class using <code>autoFake.Resolve\<ClassName\>();</code>. AutoFake will instantiate the class as Autofac would, except it will pass in default fakes for all the constructor parameters. Default method fakes simply return the default of the method return type. You can override the default to return something specific fakes simply return the default (unless you've used <code>autoFake.Provide()</code> to substitute your own customized fake for a type).
- Override default fakes to return something specific using <code>A.CallTo(...).Returns(...)</code>.
- Capture values passed into a fake method using <code>A.CallTo(...).Invokes((string value) => passedValue = value)</code>, where the faked method takes a string, and passedValue is a local variable declared in the test. After triggering the call, your test can Assert on the value in passedValue. If the faked method takes more than four parameters the syntax is a little different (see [FakeItEasy docs](https://fakeiteasy.readthedocs.io/en/stable/invoking-custom-code/)). 
- If you need to customize a fake object extensively, such as set properties on it, you can get an instace, customize it, and then use <code>autoFake.Provide\<IInterfaceName\>(instance)</code> to register it with AutoFake. Then any object that needs an <code>IInterfaceName</code> injected will get the instance you provided.
- To capture HTTP requests, you can <code>.Provide()</code> an <code>HttpClient</code> that uses a fake <code>DelegatingHandler</code> that's been overridden to capture calls. See LogHandlerTests.cs for an example.
- You CANNOT fake methods that are static or non-virtual, so it's best to fake an interface. If you need to fake a class that has no interface, feel free to create an interface for it!
- Share fakes: If you find that multiple tests in a class are using similar fakes, put a shared fake in a class field and initialize it in the constructor. Each test can override or customize the shared fake's methods as necessary. VSTest creates a new instance of the class for each test method it runs, so interaction won't be a problem. If you use similar fakes across multiple classes in a test project, put them in a common base class. 

<a id="resources">**Resources**</a>
- FakeItEasy docs: https://fakeiteasy.readthedocs.io/en/stable/
- Autofac docs: https://autofaccn.readthedocs.io/en/latest/
- Autofac's FakeItEasy introduction: https://autofaccn.readthedocs.io/en/latest/integration/fakeiteasy.html

<a id="seeAlso">**See Also**</a>
- [Testing Best Practices](../BESTPRACTICES.md)