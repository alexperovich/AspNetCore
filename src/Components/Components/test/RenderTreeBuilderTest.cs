// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Components.Test.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Microsoft.AspNetCore.Components.Test
{
    public class RenderTreeBuilderTest
    {
        [Fact]
        public void RequiresNonnullRenderer()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                new RenderTreeBuilder(null);
            });
        }

        [Fact]
        public void StartsEmpty()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());

            // Assert
            var frames = builder.GetFrames();
            Assert.NotNull(frames.Array);
            Assert.Empty(frames.AsEnumerable());
        }

        [Fact]
        public void CanAddText()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());
            var nullString = (string)null;

            // Act
            builder.AddContent(0, "First item");
            builder.AddContent(0, nullString);
            builder.AddContent(0, "Second item");

            // Assert
            var frames = builder.GetFrames();
            Assert.Collection(frames.AsEnumerable(),
                frame => AssertFrame.Text(frame, "First item"),
                frame => AssertFrame.Text(frame, string.Empty),
                frame => AssertFrame.Text(frame, "Second item"));
        }

        [Fact]
        public void CanAddMarkup()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());

            // Act
            builder.OpenElement(0, "some elem");
            builder.AddMarkupContent(1, "Blah");
            builder.AddMarkupContent(2, string.Empty);
            builder.CloseElement();

            // Assert
            var frames = builder.GetFrames();
            Assert.Collection(frames.AsEnumerable(),
                frame => AssertFrame.Element(frame, "some elem", 3),
                frame => AssertFrame.Markup(frame, "Blah"),
                frame => AssertFrame.Markup(frame, string.Empty));
        }

        [Fact]
        public void CanAddMarkupViaMarkupString()
        {
            // This represents putting @someMarkupString into the component,
            // as opposed to calling builder.AddMarkupContent directly.

            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());

            // Act - can use either constructor or cast
            builder.AddContent(0, (MarkupString)"Some markup");
            builder.AddContent(1, new MarkupString(null));

            // Assert
            var frames = builder.GetFrames();
            Assert.Collection(frames.AsEnumerable(),
                frame => AssertFrame.Markup(frame, "Some markup"),
                frame => AssertFrame.Markup(frame, string.Empty));
        }

        [Fact]
        public void CanAddNullMarkup()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());

            // Act
            builder.AddMarkupContent(0, null);

            // Assert
            var frames = builder.GetFrames();
            Assert.Collection(frames.AsEnumerable(),
                frame => AssertFrame.Markup(frame, string.Empty));
        }

        [Fact]
        public void CanAddNonStringValueAsText()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());
            var nullObject = (object)null;

            // Act
            builder.AddContent(0, 1234);
            builder.AddContent(0, nullObject);

            // Assert
            var frames = builder.GetFrames();
            Assert.Collection(frames.AsEnumerable(),
                frame => AssertFrame.Text(frame, "1234"),
                frame => AssertFrame.Text(frame, string.Empty));
        }

        [Fact]
        public void UnclosedElementsHaveNoSubtreeLength()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());

            // Act
            builder.OpenElement(0, "my element");

            // Assert
            var frame = builder.GetFrames().AsEnumerable().Single();
            AssertFrame.Element(frame, "my element", 0);
        }

        [Fact]
        public void ClosedEmptyElementsHaveSubtreeLengthOne()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());

            // Act
            builder.AddContent(0, "some frame so that the element isn't at position zero");
            builder.OpenElement(0, "my element");
            builder.CloseElement();

            // Assert
            var frames = builder.GetFrames();
            Assert.Equal(2, frames.Count);
            AssertFrame.Element(frames.Array[1], "my element", 1);
        }

        [Fact]
        public void ClosedElementsHaveCorrectSubtreeLength()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());

            // Act
            builder.OpenElement(0, "my element");
            builder.AddContent(0, "child 1");
            builder.AddContent(0, "child 2");
            builder.CloseElement();
            builder.AddContent(0, "unrelated item");

            // Assert
            var frames = builder.GetFrames();
            Assert.Equal(4, frames.Count);
            AssertFrame.Element(frames.Array[0], "my element", 3);
        }

        [Fact]
        public void CanNestElements()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());

            // Act
            builder.AddContent(0, "standalone text 1"); //  0: standalone text 1
            builder.OpenElement(0, "root");             //  1: <root>
            builder.AddContent(0, "root text 1");       //  2:     root text 1
            builder.AddContent(0, "root text 2");       //  3:     root text 2
            builder.OpenElement(0, "child");            //  4:     <child>
            builder.AddContent(0, "child text");        //  5:         child text
            builder.OpenElement(0, "grandchild");       //  6:         <grandchild>
            builder.AddContent(0, "grandchild text 1"); //  7:             grandchild text 1
            builder.AddContent(0, "grandchild text 2"); //  8:             grandchild text 2
            builder.CloseElement();                     //             </grandchild>
            builder.CloseElement();                     //         </child>
            builder.AddContent(0, "root text 3");       //  9:     root text 3
            builder.OpenElement(0, "child 2");          // 10:     <child 2>
            builder.CloseElement();                     //         </child 2>
            builder.CloseElement();                     //      </root>
            builder.AddContent(0, "standalone text 2"); // 11:  standalone text 2

            // Assert
            Assert.Collection(builder.GetFrames().AsEnumerable(),
                frame => AssertFrame.Text(frame, "standalone text 1"),
                frame => AssertFrame.Element(frame, "root", 10),
                frame => AssertFrame.Text(frame, "root text 1"),
                frame => AssertFrame.Text(frame, "root text 2"),
                frame => AssertFrame.Element(frame, "child", 5),
                frame => AssertFrame.Text(frame, "child text"),
                frame => AssertFrame.Element(frame, "grandchild", 3),
                frame => AssertFrame.Text(frame, "grandchild text 1"),
                frame => AssertFrame.Text(frame, "grandchild text 2"),
                frame => AssertFrame.Text(frame, "root text 3"),
                frame => AssertFrame.Element(frame, "child 2", 1),
                frame => AssertFrame.Text(frame, "standalone text 2"));
        }

        [Fact]
        public void CanAddAttributes()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());
            Action<UIEventArgs> eventHandler = eventInfo => { };

            // Act
            builder.OpenElement(0, "myelement");                    //  0: <myelement
            builder.AddAttribute(0, "attribute1", "value 1");       //  1:     attribute1="value 1"
            builder.AddAttribute(0, "attribute2", 123);             //  2:     attribute2=intExpression123>
            builder.OpenElement(0, "child");                        //  3:   <child
            builder.AddAttribute(0, "childevent", eventHandler);    //  4:       childevent=eventHandler>
            builder.AddContent(0, "some text");                     //  5:     some text
            builder.CloseElement();                                 //       </child>
            builder.CloseElement();                                 //     </myelement>

            // Assert
            Assert.Collection(builder.GetFrames().AsEnumerable(),
                frame => AssertFrame.Element(frame, "myelement", 6),
                frame => AssertFrame.Attribute(frame, "attribute1", "value 1"),
                frame => AssertFrame.Attribute(frame, "attribute2", "123"),
                frame => AssertFrame.Element(frame, "child", 3),
                frame => AssertFrame.Attribute(frame, "childevent", eventHandler),
                frame => AssertFrame.Text(frame, "some text"));
        }

        [Fact]
        public void CanAddMultipleAttributes_AllowsNull()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());

            // Act
            builder.OpenElement(0, "myelement");
            builder.AddMultipleAttributes(0, null);
            builder.CloseElement();

            // Assert
            var frames = builder.GetFrames().AsEnumerable().ToArray();
            Assert.Collection(
                frames,
                frame => AssertFrame.Element(frame, "myelement", 1));
        }

        [Fact]
        public void CanAddMultipleAttributes_InterspersedWithOtherAttributes()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());
            Action<UIEventArgs> eventHandler = eventInfo => { };

            // Act
            builder.OpenElement(0, "myelement");
            builder.AddAttribute(0, "attribute1", "value 1");
            builder.AddMultipleAttributes(0, new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                { "attribute1", "test1" },
                { "attribute2", true },
                { "attribute3", eventHandler },
            });
            builder.AddAttribute(0, "ATTRIBUTE2", true);
            builder.AddMultipleAttributes(0, new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                { "attribute4", "test4" },
                { "attribute5", false },
                { "attribute6", eventHandler },
            });

            // Null or false values don't create frames of their own, but they can
            // "knock out" earlier values.
            builder.AddAttribute(0, "attribute6", false);
            builder.AddAttribute(0, "attribute4", (string)null);

            builder.AddAttribute(0, "attribute7", "the end");
            builder.CloseElement();

            // Assert
            var frames = builder.GetFrames().AsEnumerable().ToArray();
            Assert.Collection(
                frames,
                frame => AssertFrame.Element(frame, "myelement", 5),
                frame => AssertFrame.Attribute(frame, "attribute1", "test1"),
                frame => AssertFrame.Attribute(frame, "attribute3", eventHandler),
                frame => AssertFrame.Attribute(frame, "ATTRIBUTE2", true),
                frame => AssertFrame.Attribute(frame, "attribute7", "the end"));
        }

        [Fact]
        public void CanAddMultipleAttributes_DictionaryObject()
        {
            var attributes = new Dictionary<string, object>
            {
                { "attribute1", "test1" },
                { "attribute2", "123" },
                { "attribute3", true },
            };

            // Act & Assert
            CanAddMultipleAttributesTest(attributes);
        }

        [Fact]
        public void CanAddMultipleAttributes_IReadOnlyDictionaryObject()
        {
            var attributes = new Dictionary<string, object>
            {
                { "attribute1", "test1" },
                { "attribute2", "123" },
                { "attribute3", true },
            };

            // Act & Assert
            CanAddMultipleAttributesTest((IReadOnlyDictionary<string, object>)attributes);
        }

        [Fact]
        public void CanAddMultipleAttributes_ListKvpObject()
        {
            var attributes = new List<KeyValuePair<string, object>>()
            {
                new KeyValuePair<string, object>("attribute1", "test1"),
                new KeyValuePair<string, object>("attribute2", "123"),
                new KeyValuePair<string, object>("attribute3", true),
            };

            // Act & Assert
            CanAddMultipleAttributesTest(attributes);
        }

        [Fact]
        public void CanAddMultipleAttributes_ArrayKvpObject()
        {
            var attributes = new KeyValuePair<string, object>[]
            {
                new KeyValuePair<string, object>("attribute1", "test1"),
                new KeyValuePair<string, object>("attribute2", "123"),
                new KeyValuePair<string, object>("attribute3", true),
            };

            // Act & Assert
            CanAddMultipleAttributesTest(attributes);
        }

        private void CanAddMultipleAttributesTest(IEnumerable<KeyValuePair<string, object>> attributes)
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());

            // Act
            builder.OpenElement(0, "myelement");
            builder.AddMultipleAttributes(0, attributes);
            builder.CloseElement();

            // Assert
            var frames = builder.GetFrames().AsEnumerable().ToArray();

            var i = 1;
            foreach (var attribute in attributes)
            {
                var frame = frames[i++];
                AssertFrame.Attribute(frame, attribute.Key, attribute.Value);
            }
        }

        [Fact]
        public void CannotAddAttributeAtRoot()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());

            // Act/Assert
            Assert.Throws<InvalidOperationException>(() =>
            {
                builder.AddAttribute(0, "name", "value");
            });
        }

        [Fact]
        public void CannotAddDelegateAttributeAtRoot()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());

            // Act/Assert
            Assert.Throws<InvalidOperationException>(() =>
            {
                builder.AddAttribute(0, "name", new Action<string>(text => { }));
            });
        }

        [Fact]
        public void CannotAddAttributeToText()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());

            // Act/Assert
            Assert.Throws<InvalidOperationException>(() =>
            {
                builder.OpenElement(0, "some element");
                builder.AddContent(1, "hello");
                builder.AddAttribute(2, "name", "value");
            });
        }

        [Fact]
        public void CannotAddEventHandlerAttributeToText()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());

            // Act/Assert
            Assert.Throws<InvalidOperationException>(() =>
            {
                builder.OpenElement(0, "some element");
                builder.AddContent(1, "hello");
                builder.AddAttribute(2, "name", new Action<UIEventArgs>(eventInfo => { }));
            });
        }

        [Fact]
        public void CannotAddAttributeToRegion()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());

            // Act/Assert
            Assert.Throws<InvalidOperationException>(() =>
            {
                builder.OpenRegion(0);
                builder.AddAttribute(1, "name", "value");
            });
        }

        [Fact]
        public void CannotAddAttributeToElementReferenceCapture()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());

            // Act/Assert
            Assert.Throws<InvalidOperationException>(() =>
            {
                builder.OpenElement(0, "some element");
                builder.AddElementReferenceCapture(1, _ => { });
                builder.AddAttribute(2, "name", "value");
            });
        }

        [Fact]
        public void CannotAddAttributeToComponentReferenceCapture()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());

            // Act/Assert
            Assert.Throws<InvalidOperationException>(() =>
            {
                builder.OpenComponent<TestComponent>(0);
                builder.AddComponentReferenceCapture(1, _ => { });
                builder.AddAttribute(2, "name", "value");
            });
        }

        [Fact]
        public void CanAddChildComponentsUsingGenericParam()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());

            // Act
            builder.OpenElement(10, "parent");                   //  0: <parent>
            builder.OpenComponent<TestComponent>(11);            //  1:     <testcomponent
            builder.AddAttribute(12, "child1attribute1", "A");   //  2:       child1attribute1="A"
            builder.AddAttribute(13, "child1attribute2", "B");   //  3:       child1attribute2="B">
            builder.CloseComponent();                            //         </testcomponent>
            builder.OpenComponent<TestComponent>(14);            //  4:     <testcomponent
            builder.AddAttribute(15, "child2attribute", "C");    //  5:       child2attribute="C">
            builder.CloseComponent();                            //         </testcomponent>
            builder.CloseElement();                              //     </parent>

            // Assert
            Assert.Collection(builder.GetFrames().AsEnumerable(),
                frame => AssertFrame.Element(frame, "parent", 6),
                frame => AssertFrame.Component<TestComponent>(frame),
                frame => AssertFrame.Attribute(frame, "child1attribute1", "A"),
                frame => AssertFrame.Attribute(frame, "child1attribute2", "B"),
                frame => AssertFrame.Component<TestComponent>(frame),
                frame => AssertFrame.Attribute(frame, "child2attribute", "C"));
        }

        [Fact]
        public void CanAddChildComponentsUsingTypeArgument()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());

            // Act
            var componentType = typeof(TestComponent);
            builder.OpenElement(10, "parent");                   //  0: <parent>
            builder.OpenComponent(11, componentType);            //  1:     <testcomponent
            builder.AddAttribute(12, "child1attribute1", "A");   //  2:       child1attribute1="A"
            builder.AddAttribute(13, "child1attribute2", "B");   //  3:       child1attribute2="B">
            builder.CloseComponent();                            //         </testcomponent>
            builder.OpenComponent(14, componentType);            //  4:     <testcomponent
            builder.AddAttribute(15, "child2attribute", "C");    //  5:       child2attribute="C">
            builder.CloseComponent();                            //         </testcomponent>
            builder.CloseElement();                              //     </parent>

            // Assert
            Assert.Collection(builder.GetFrames().AsEnumerable(),
                frame => AssertFrame.Element(frame, "parent", 6),
                frame => AssertFrame.Component<TestComponent>(frame),
                frame => AssertFrame.Attribute(frame, "child1attribute1", "A"),
                frame => AssertFrame.Attribute(frame, "child1attribute2", "B"),
                frame => AssertFrame.Component<TestComponent>(frame),
                frame => AssertFrame.Attribute(frame, "child2attribute", "C"));
        }

        [Fact]
        public void CanAddRegions()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());

            // Act
            builder.OpenElement(10, "parent");                      //  0: <parent>
            builder.OpenRegion(11);                                 //  1:     [region
            builder.AddContent(3, "Hello");                         //  2:         Hello
            builder.OpenRegion(4);                                  //  3:         [region
            builder.OpenElement(3, "another");                      //  4:             <another>
            builder.CloseElement();                                 //                 </another>
            builder.CloseRegion();                                  //             ]
            builder.AddContent(6, "Goodbye");                       //  5:         Goodbye
            builder.CloseRegion();                                  //         ]
            builder.CloseElement();                                 //     </parent>

            // Assert
            Assert.Collection(builder.GetFrames().AsEnumerable(),
                frame => AssertFrame.Element(frame, "parent", 6, 10),
                frame => AssertFrame.Region(frame, 5, 11),
                frame => AssertFrame.Text(frame, "Hello", 3),
                frame => AssertFrame.Region(frame, 2, 4),
                frame => AssertFrame.Element(frame, "another", 1, 3),
                frame => AssertFrame.Text(frame, "Goodbye", 6));
        }

        [Fact]
        public void CanAddFragments()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());
            RenderFragment fragment = fragmentBuilder =>
            {
                fragmentBuilder.AddContent(0, "Hello from the fragment");
                fragmentBuilder.OpenElement(1, "Fragment element");
                fragmentBuilder.AddContent(2, "Some text");
                fragmentBuilder.CloseElement();
            };

            // Act
            builder.OpenElement(10, "parent");
            builder.AddContent(11, fragment);
            builder.CloseElement();

            // Assert
            Assert.Collection(builder.GetFrames().AsEnumerable(),
                frame => AssertFrame.Element(frame, "parent", 5, 10),
                frame => AssertFrame.Region(frame, 4, 11),
                frame => AssertFrame.Text(frame, "Hello from the fragment", 0),
                frame => AssertFrame.Element(frame, "Fragment element", 2, 1),
                frame => AssertFrame.Text(frame, "Some text", 2));
        }

        [Fact]
        public void CanAddElementReferenceCaptureInsideElement()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());
            Action<ElementRef> referenceCaptureAction = elementRef => { };

            // Act
            builder.OpenElement(0, "myelement");                    //  0: <myelement
            builder.AddAttribute(1, "attribute2", 123);             //  1:     attribute2=intExpression123>
            builder.AddElementReferenceCapture(2, referenceCaptureAction); //  2:     # capture: referenceCaptureAction
            builder.AddContent(3, "some text");                     //  3:     some text
            builder.CloseElement();                                 //     </myelement>

            // Assert
            Assert.Collection(builder.GetFrames().AsEnumerable(),
                frame => AssertFrame.Element(frame, "myelement", 4, 0),
                frame => AssertFrame.Attribute(frame, "attribute2", "123", 1),
                frame => AssertFrame.ElementReferenceCapture(frame, referenceCaptureAction, 2),
                frame => AssertFrame.Text(frame, "some text", 3));
        }

        [Fact]
        public void CannotAddElementReferenceCaptureWithNoParent()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());

            // Act/Assert
            Assert.Throws<InvalidOperationException>(() =>
            {
                builder.AddElementReferenceCapture(0, _ => { });
            });
        }

        [Fact]
        public void CannotAddElementReferenceCaptureInsideComponent()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());

            // Act/Assert
            Assert.Throws<InvalidOperationException>(() =>
            {
                builder.OpenComponent<TestComponent>(0);
                builder.AddElementReferenceCapture(1, _ => { });
            });
        }

        [Fact]
        public void CannotAddElementReferenceCaptureInsideRegion()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());

            // Act/Assert
            Assert.Throws<InvalidOperationException>(() =>
            {
                builder.OpenRegion(0);
                builder.AddElementReferenceCapture(1, _ => { });
            });
        }

        [Fact]
        public void CanAddMultipleReferenceCapturesToSameElement()
        {
            // There won't be any way of doing this from Razor because there's no known use
            // case for it. However it's harder to *not* support it than to support it, and
            // there's no known reason to prevent it, so here's test coverage to show it
            // just works.

            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());
            Action<ElementRef> referenceCaptureAction1 = elementRef => { };
            Action<ElementRef> referenceCaptureAction2 = elementRef => { };

            // Act
            builder.OpenElement(0, "myelement");
            builder.AddElementReferenceCapture(0, referenceCaptureAction1);
            builder.AddElementReferenceCapture(0, referenceCaptureAction2);
            builder.CloseElement();

            // Assert
            Assert.Collection(builder.GetFrames().AsEnumerable(),
                frame => AssertFrame.Element(frame, "myelement", 3),
                frame => AssertFrame.ElementReferenceCapture(frame, referenceCaptureAction1),
                frame => AssertFrame.ElementReferenceCapture(frame, referenceCaptureAction2));
        }

        [Fact]
        public void CanAddComponentReferenceCaptureInsideComponent()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());
            Action<object> myAction = elementRef => { };

            // Act
            builder.OpenComponent<TestComponent>(0);                //  0: <TestComponent
            builder.AddAttribute(1, "attribute2", 123);             //  1:     attribute2=intExpression123>
            builder.AddComponentReferenceCapture(2, myAction);      //  2:     # capture: myAction
            builder.AddContent(3, "some text");                     //  3:     some text
            builder.CloseComponent();                               //     </TestComponent>

            // Assert
            Assert.Collection(builder.GetFrames().AsEnumerable(),
                frame => AssertFrame.Component<TestComponent>(frame, 4, 0),
                frame => AssertFrame.Attribute(frame, "attribute2", 123, 1),
                frame => AssertFrame.ComponentReferenceCapture(frame, myAction, 2),
                frame => AssertFrame.Text(frame, "some text", 3));
        }

        [Fact]
        public void CannotAddComponentReferenceCaptureWithNoParent()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());

            // Act/Assert
            Assert.Throws<InvalidOperationException>(() =>
            {
                builder.AddComponentReferenceCapture(0, _ => { });
            });
        }

        [Fact]
        public void CannotAddComponentReferenceCaptureInsideElement()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());

            // Act/Assert
            Assert.Throws<InvalidOperationException>(() =>
            {
                builder.OpenElement(0, "myelement");
                builder.AddComponentReferenceCapture(1, _ => { });
            });
        }

        [Fact]
        public void CannotAddComponentReferenceCaptureInsideRegion()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());

            // Act/Assert
            Assert.Throws<InvalidOperationException>(() =>
            {
                builder.OpenRegion(0);
                builder.AddComponentReferenceCapture(1, _ => { });
            });
        }

        [Fact]
        public void CanAddMultipleReferenceCapturesToSameComponent()
        {
            // There won't be any way of doing this from Razor because there's no known use
            // case for it. However it's harder to *not* support it than to support it, and
            // there's no known reason to prevent it, so here's test coverage to show it
            // just works.

            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());
            Action<object> referenceCaptureAction1 = elementRef => { };
            Action<object> referenceCaptureAction2 = elementRef => { };

            // Act
            builder.OpenComponent<TestComponent>(0);
            builder.AddComponentReferenceCapture(0, referenceCaptureAction1);
            builder.AddComponentReferenceCapture(0, referenceCaptureAction2);
            builder.CloseComponent();

            // Assert
            Assert.Collection(builder.GetFrames().AsEnumerable(),
                frame => AssertFrame.Component<TestComponent>(frame, 3),
                frame => AssertFrame.ComponentReferenceCapture(frame, referenceCaptureAction1),
                frame => AssertFrame.ComponentReferenceCapture(frame, referenceCaptureAction2));
        }

        [Fact]
        public void CanClear()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());

            // Act
            builder.AddContent(0, "some text");
            builder.OpenElement(1, "elem");
            builder.AddContent(2, "more text");
            builder.CloseElement();
            builder.Clear();

            // Assert
            Assert.Empty(builder.GetFrames().AsEnumerable());
        }

        [Fact]
        public void AddAttribute_Element_BoolTrue_AddsFrame()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());

            // Act
            builder.OpenElement(0, "elem");
            builder.AddAttribute(1, "attr", true);
            builder.CloseElement();

            // Assert
            Assert.Collection(
                builder.GetFrames().AsEnumerable(),
                frame => AssertFrame.Element(frame, "elem", 2, 0),
                frame => AssertFrame.Attribute(frame, "attr", true, 1));
        }

        [Fact]
        public void AddAttribute_Element_BoolFalse_IgnoresFrame()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());

            // Act
            builder.OpenElement(0, "elem");
            builder.AddAttribute(1, "attr", false);
            builder.CloseElement();

            // Assert
            Assert.Collection(
                builder.GetFrames().AsEnumerable(),
                frame => AssertFrame.Element(frame, "elem", 1, 0));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void AddAttribute_Component_Bool_SetsAttributeValue(bool value)
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());

            // Act
            builder.OpenComponent<TestComponent>(0);
            builder.AddAttribute(1, "attr", value);
            builder.CloseComponent();

            // Assert
            Assert.Collection(
                builder.GetFrames().AsEnumerable(),
                frame => AssertFrame.Component<TestComponent>(frame, 2, 0),
                frame => AssertFrame.Attribute(frame, "attr", value, 1));
        }

        [Fact]
        public void AddAttribute_Element_StringValue_AddsFrame()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());

            // Act
            builder.OpenElement(0, "elem");
            builder.AddAttribute(1, "attr", "hi");
            builder.CloseElement();

            // Assert
            Assert.Collection(
                builder.GetFrames().AsEnumerable(),
                frame => AssertFrame.Element(frame, "elem", 2, 0),
                frame => AssertFrame.Attribute(frame, "attr", "hi", 1));
        }

        [Fact]
        public void AddAttribute_Element_StringNull_IgnoresFrame()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());

            // Act
            builder.OpenElement(0, "elem");
            builder.AddAttribute(1, "attr", (string)null);
            builder.CloseElement();

            // Assert
            Assert.Collection(
                builder.GetFrames().AsEnumerable(),
                frame => AssertFrame.Element(frame, "elem", 1, 0));
        }

        [Theory]
        [InlineData("hi")]
        [InlineData(null)]
        public void AddAttribute_Component_StringValue_SetsAttributeValue(string value)
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());

            // Act
            builder.OpenComponent<TestComponent>(0);
            builder.AddAttribute(1, "attr", value);
            builder.CloseComponent();

            // Assert
            Assert.Collection(
                builder.GetFrames().AsEnumerable(),
                frame => AssertFrame.Component<TestComponent>(frame, 2, 0),
                frame => AssertFrame.Attribute(frame, "attr", value, 1));
        }

        [Fact]
        public void AddAttribute_Element_EventHandler_AddsFrame()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());

            var value = new Action<UIEventArgs>((e) => { });

            // Act
            builder.OpenElement(0, "elem");
            builder.AddAttribute(1, "attr", value);
            builder.CloseElement();

            // Assert
            Assert.Collection(
                builder.GetFrames().AsEnumerable(),
                frame => AssertFrame.Element(frame, "elem", 2, 0),
                frame => AssertFrame.Attribute(frame, "attr", value, 1));
        }

        [Fact]
        public void AddAttribute_Element_NullEventHandler_IgnoresFrame()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());

            // Act
            builder.OpenElement(0, "elem");
            builder.AddAttribute(1, "attr", (Action<UIEventArgs>)null);
            builder.CloseElement();

            // Assert
            Assert.Collection(
                builder.GetFrames().AsEnumerable(),
                frame => AssertFrame.Element(frame, "elem", 1, 0));
        }

        [Fact]
        public void AddAttribute_Element_Action_AddsFrame()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());

            var value = new Action(() => { });

            // Act
            builder.OpenElement(0, "elem");
            builder.AddAttribute(1, "attr", value);
            builder.CloseElement();

            // Assert
            Assert.Collection(
                builder.GetFrames().AsEnumerable(),
                frame => AssertFrame.Element(frame, "elem", 2, 0),
                frame => AssertFrame.Attribute(frame, "attr", value, 1));
        }

        [Fact]
        public void AddAttribute_Element_NullAction_IgnoresFrame()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());

            // Act
            builder.OpenElement(0, "elem");
            builder.AddAttribute(1, "attr", (Action)null);
            builder.CloseElement();

            // Assert
            Assert.Collection(
                builder.GetFrames().AsEnumerable(),
                frame => AssertFrame.Element(frame, "elem", 1, 0));
        }

        public static TheoryData<Action<UIEventArgs>> EventHandlerValues => new TheoryData<Action<UIEventArgs>>
        {
            null,
            (e) => { },
        };

        [Theory]
        [MemberData(nameof(EventHandlerValues))]
        public void AddAttribute_Component_EventHandlerValue_SetsAttributeValue(Action<UIEventArgs> value)
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());

            // Act
            builder.OpenComponent<TestComponent>(0);
            builder.AddAttribute(1, "attr", value);
            builder.CloseComponent();

            // Assert
            Assert.Collection(
                builder.GetFrames().AsEnumerable(),
                frame => AssertFrame.Component<TestComponent>(frame, 2, 0),
                frame => AssertFrame.Attribute(frame, "attr", value, 1));
        }

        [Fact]
        public void AddAttribute_Element_EventCallback_AddsFrame()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());
            var callback = new EventCallback(null, new Action(() => { }));

            // Act
            builder.OpenElement(0, "elem");
            builder.AddAttribute(1, "attr", callback);
            builder.CloseElement();

            // Assert
            Assert.Collection(
                builder.GetFrames().AsEnumerable(),
                frame => AssertFrame.Element(frame, "elem", 2, 0),
                frame => AssertFrame.Attribute(frame, "attr", callback.Delegate, 1));
        }

        [Fact]
        public void AddAttribute_Element_EventCallback_Default_DoesNotAddFrame()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());
            var callback = default(EventCallback);

            // Act
            builder.OpenElement(0, "elem");
            builder.AddAttribute(1, "attr", callback);
            builder.CloseElement();

            // Assert
            Assert.Collection(
                builder.GetFrames().AsEnumerable(),
                frame => AssertFrame.Element(frame, "elem", 1, 0));
        }

        [Fact]
        public void AddAttribute_Element_EventCallbackWithReceiver_AddsFrame()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());
            var receiver = Mock.Of<IHandleEvent>();
            var callback = new EventCallback(receiver, new Action(() => { }));

            // Act
            builder.OpenElement(0, "elem");
            builder.AddAttribute(1, "attr", callback);
            builder.CloseElement();

            // Assert
            Assert.Collection(
                builder.GetFrames().AsEnumerable(),
                frame => AssertFrame.Element(frame, "elem", 2, 0),
                frame => AssertFrame.Attribute(frame, "attr", callback, 1));
        }

        [Fact]
        public void AddAttribute_Component_EventCallback_AddsFrame()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());
            var receiver = Mock.Of<IHandleEvent>();
            var callback = new EventCallback(receiver, new Action(() => { }));

            // Act
            builder.OpenComponent<TestComponent>(0);
            builder.AddAttribute(1, "attr", callback);
            builder.CloseComponent();

            // Assert
            Assert.Collection(
                builder.GetFrames().AsEnumerable(),
                frame => AssertFrame.Component<TestComponent>(frame, 2, 0),
                frame => AssertFrame.Attribute(frame, "attr", callback, 1));
        }

        [Fact]
        public void AddAttribute_Element_EventCallbackOfT_AddsFrame()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());
            var callback = new EventCallback<string>(null, new Action<string>((s) => { }));

            // Act
            builder.OpenElement(0, "elem");
            builder.AddAttribute(1, "attr", callback);
            builder.CloseElement();

            // Assert
            Assert.Collection(
                builder.GetFrames().AsEnumerable(),
                frame => AssertFrame.Element(frame, "elem", 2, 0),
                frame => AssertFrame.Attribute(frame, "attr", callback.Delegate, 1));
        }

        [Fact]
        public void AddAttribute_Element_EventCallbackOfT_Default_DoesNotAddFrame()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());
            var callback = default(EventCallback<string>);

            // Act
            builder.OpenElement(0, "elem");
            builder.AddAttribute(1, "attr", callback);
            builder.CloseElement();

            // Assert
            Assert.Collection(
                builder.GetFrames().AsEnumerable(),
                frame => AssertFrame.Element(frame, "elem", 1, 0));
        }

        [Fact]
        public void AddAttribute_Element_EventCallbackWithReceiverOfT_AddsFrame()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());
            var receiver = Mock.Of<IHandleEvent>();
            var callback = new EventCallback<string>(receiver, new Action<string>((s) => { }));

            // Act
            builder.OpenElement(0, "elem");
            builder.AddAttribute(1, "attr", callback);
            builder.CloseElement();

            // Assert
            Assert.Collection(
                builder.GetFrames().AsEnumerable(),
                frame => AssertFrame.Element(frame, "elem", 2, 0),
                frame => AssertFrame.Attribute(frame, "attr", new EventCallback(callback.Receiver, callback.Delegate), 1));
        }

        [Fact]
        public void AddAttribute_Component_EventCallbackOfT_AddsFrame()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());
            var receiver = Mock.Of<IHandleEvent>();
            var callback = new EventCallback<string>(receiver, new Action<string>((s) => { }));

            // Act
            builder.OpenComponent<TestComponent>(0);
            builder.AddAttribute(1, "attr", callback);
            builder.CloseComponent();

            // Assert
            Assert.Collection(
                builder.GetFrames().AsEnumerable(),
                frame => AssertFrame.Component<TestComponent>(frame, 2, 0),
                frame => AssertFrame.Attribute(frame, "attr", callback, 1));
        }

        [Fact]
        public void AddAttribute_Element_ObjectBoolTrue_AddsFrame()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());

            // Act
            builder.OpenElement(0, "elem");
            builder.AddAttribute(1, "attr", (object)true);
            builder.CloseElement();

            // Assert
            Assert.Collection(
                builder.GetFrames().AsEnumerable(),
                frame => AssertFrame.Element(frame, "elem", 2, 0),
                frame => AssertFrame.Attribute(frame, "attr", true, 1));
        }

        [Fact]
        public void AddAttribute_Element_ObjectBoolFalse_IgnoresFrame()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());

            // Act
            builder.OpenElement(0, "elem");
            builder.AddAttribute(1, "attr", (object)false);
            builder.CloseElement();

            // Assert
            Assert.Collection(
                builder.GetFrames().AsEnumerable(),
                frame => AssertFrame.Element(frame, "elem", 1, 0));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void AddAttribute_Component_ObjectBoolValue_SetsAttributeValue(bool value)
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());

            // Act
            builder.OpenComponent<TestComponent>(0);
            builder.AddAttribute(1, "attr", (object)value);
            builder.CloseComponent();

            // Assert
            Assert.Collection(
                builder.GetFrames().AsEnumerable(),
                frame => AssertFrame.Component<TestComponent>(frame, 2, 0),
                frame => AssertFrame.Attribute(frame, "attr", value, 1));
        }

        [Fact]
        public void AddAttribute_Element_ObjectStringValue_AddsFrame()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());

            // Act
            builder.OpenElement(0, "elem");
            builder.AddAttribute(1, "attr", (object)"hi");
            builder.CloseElement();

            // Assert
            Assert.Collection(
                builder.GetFrames().AsEnumerable(),
                frame => AssertFrame.Element(frame, "elem", 2, 0),
                frame => AssertFrame.Attribute(frame, "attr", "hi", 1));
        }

        [Fact]
        public void AddAttribute_Component_ObjectStringValue_SetsAttributeValue()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());

            // Act
            builder.OpenComponent<TestComponent>(0);
            builder.AddAttribute(1, "attr", (object)"hi");
            builder.CloseComponent();

            // Assert
            Assert.Collection(
                builder.GetFrames().AsEnumerable(),
                frame => AssertFrame.Component<TestComponent>(frame, 2, 0),
                frame => AssertFrame.Attribute(frame, "attr", "hi", 1));
        }

        [Fact]
        public void AddAttribute_Element_ObjectEventHandler_AddsFrame()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());

            var value = new Action<UIEventArgs>((e) => { });

            // Act
            builder.OpenElement(0, "elem");
            builder.AddAttribute(1, "attr", (object)value);
            builder.CloseElement();

            // Assert
            Assert.Collection(
                builder.GetFrames().AsEnumerable(),
                frame => AssertFrame.Element(frame, "elem", 2, 0),
                frame => AssertFrame.Attribute(frame, "attr", value, 1));
        }

        [Fact]
        public void AddAttribute_Component_ObjectUIEventHandleValue_SetsAttributeValue()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());

            var value = new Action<UIEventArgs>((e) => { });

            // Act
            builder.OpenComponent<TestComponent>(0);
            builder.AddAttribute(1, "attr", (object)value);
            builder.CloseComponent();

            // Assert
            Assert.Collection(
                builder.GetFrames().AsEnumerable(),
                frame => AssertFrame.Component<TestComponent>(frame, 2, 0),
                frame => AssertFrame.Attribute(frame, "attr", value, 1));
        }

        [Fact]
        public void AddAttribute_Element_ObjectAction_AddsFrame()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());

            var value = new Action(() => { });

            // Act
            builder.OpenElement(0, "elem");
            builder.AddAttribute(1, "attr", (object)value);
            builder.CloseElement();

            // Assert
            Assert.Collection(
                builder.GetFrames().AsEnumerable(),
                frame => AssertFrame.Element(frame, "elem", 2, 0),
                frame => AssertFrame.Attribute(frame, "attr", value, 1));
        }

        [Fact]
        public void AddAttribute_Component_ObjectAction_SetsAttributeValue()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());

            var value = new Action(() => { });

            // Act
            builder.OpenComponent<TestComponent>(0);
            builder.AddAttribute(1, "attr", (object)value);
            builder.CloseComponent();

            // Assert
            Assert.Collection(
                builder.GetFrames().AsEnumerable(),
                frame => AssertFrame.Component<TestComponent>(frame, 2, 0),
                frame => AssertFrame.Attribute(frame, "attr", value, 1));
        }

        [Fact]
        public void AddAttribute_Element_ObjectEventCallback_AddsFrame()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());
            var callback = new EventCallback(null, new Action(() => { }));

            // Act
            builder.OpenElement(0, "elem");
            builder.AddAttribute(1, "attr", (object)callback);
            builder.CloseElement();

            // Assert
            Assert.Collection(
                builder.GetFrames().AsEnumerable(),
                frame => AssertFrame.Element(frame, "elem", 2, 0),
                frame => AssertFrame.Attribute(frame, "attr", callback.Delegate, 1));
        }

        [Fact]
        public void AddAttribute_Element_ObjectEventCallback_Default_DoesNotAddFrame()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());
            var callback = default(EventCallback);

            // Act
            builder.OpenElement(0, "elem");
            builder.AddAttribute(1, "attr", (object)callback);
            builder.CloseElement();

            // Assert
            Assert.Collection(
                builder.GetFrames().AsEnumerable(),
                frame => AssertFrame.Element(frame, "elem", 1, 0));
        }

        [Fact]
        public void AddAttribute_Element_ObjectEventCallbackWithReceiver_AddsFrame()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());
            var receiver = Mock.Of<IHandleEvent>();
            var callback = new EventCallback(receiver, new Action(() => { }));

            // Act
            builder.OpenElement(0, "elem");
            builder.AddAttribute(1, "attr", (object)callback);
            builder.CloseElement();

            // Assert
            Assert.Collection(
                builder.GetFrames().AsEnumerable(),
                frame => AssertFrame.Element(frame, "elem", 2, 0),
                frame => AssertFrame.Attribute(frame, "attr", callback, 1));
        }

        [Fact]
        public void AddAttribute_Component_ObjectEventCallback_AddsFrame()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());
            var receiver = Mock.Of<IHandleEvent>();
            var callback = new EventCallback(receiver, new Action(() => { }));

            // Act
            builder.OpenComponent<TestComponent>(0);
            builder.AddAttribute(1, "attr", (object)callback);
            builder.CloseComponent();

            // Assert
            Assert.Collection(
                builder.GetFrames().AsEnumerable(),
                frame => AssertFrame.Component<TestComponent>(frame, 2, 0),
                frame => AssertFrame.Attribute(frame, "attr", callback, 1));
        }

        [Fact]
        public void AddAttribute_Element_ObjectEventCallbackOfT_AddsFrame()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());
            var callback = new EventCallback<string>(null, new Action<string>((s) => { }));

            // Act
            builder.OpenElement(0, "elem");
            builder.AddAttribute(1, "attr", (object)callback);
            builder.CloseElement();

            // Assert
            Assert.Collection(
                builder.GetFrames().AsEnumerable(),
                frame => AssertFrame.Element(frame, "elem", 2, 0),
                frame => AssertFrame.Attribute(frame, "attr", callback.Delegate, 1));
        }

        [Fact]
        public void AddAttribute_Element_ObjectEventCallbackOfT_Default_DoesNotAddFrame()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());
            var callback = default(EventCallback<string>);

            // Act
            builder.OpenElement(0, "elem");
            builder.AddAttribute(1, "attr", (object)callback);
            builder.CloseElement();

            // Assert
            Assert.Collection(
                builder.GetFrames().AsEnumerable(),
                frame => AssertFrame.Element(frame, "elem", 1, 0));
        }

        [Fact]
        public void AddAttribute_Element_ObjectEventCallbackWithReceiverOfT_AddsFrame()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());
            var receiver = Mock.Of<IHandleEvent>();
            var callback = new EventCallback<string>(receiver, new Action<string>((s) => { }));

            // Act
            builder.OpenElement(0, "elem");
            builder.AddAttribute(1, "attr", (object)callback);
            builder.CloseElement();

            // Assert
            Assert.Collection(
                builder.GetFrames().AsEnumerable(),
                frame => AssertFrame.Element(frame, "elem", 2, 0),
                frame => AssertFrame.Attribute(frame, "attr", new EventCallback(callback.Receiver, callback.Delegate), 1));
        }

        [Fact]
        public void AddAttribute_Element_ObjectNull_IgnoresFrame()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());

            // Act
            builder.OpenElement(0, "elem");
            builder.AddAttribute(1, "attr", (object)null);
            builder.CloseElement();

            // Assert
            Assert.Collection(
                builder.GetFrames().AsEnumerable(),
                frame => AssertFrame.Element(frame, "elem", 1, 0));
        }

        [Fact]
        public void CanAddKeyToElement()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());
            var keyValue = new object();

            // Act
            builder.OpenElement(0, "elem");
            builder.AddAttribute(1, "attribute before", "before value");
            builder.SetKey(keyValue);
            builder.AddAttribute(2, "attribute after", "after value");
            builder.CloseElement();

            // Assert
            Assert.Collection(
                builder.GetFrames().AsEnumerable(),
                frame =>
                {
                    AssertFrame.Element(frame, "elem", 3, 0);
                    Assert.Same(keyValue, frame.ElementKey);
                },
                frame => AssertFrame.Attribute(frame, "attribute before", "before value", 1),
                frame => AssertFrame.Attribute(frame, "attribute after", "after value", 2));
        }

        [Fact]
        public void CanAddKeyToComponent()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());
            var keyValue = new object();

            // Act
            builder.OpenComponent<TestComponent>(0);
            builder.AddAttribute(1, "param before", 123);
            builder.SetKey(keyValue);
            builder.AddAttribute(2, "param after", 456);
            builder.CloseComponent();

            // Assert
            Assert.Collection(
                builder.GetFrames().AsEnumerable(),
                frame =>
                {
                    AssertFrame.Component<TestComponent>(frame, 3, 0);
                    Assert.Same(keyValue, frame.ComponentKey);
                },
                frame => AssertFrame.Attribute(frame, "param before", 123, 1),
                frame => AssertFrame.Attribute(frame, "param after", 456, 2));
        }

        [Fact]
        public void CannotAddKeyOutsideComponentOrElement_TreeRoot()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());

            // Act/Assert
            var ex = Assert.Throws<InvalidOperationException>(() =>
            {
                builder.SetKey(new object());
            });
            Assert.Equal("Cannot set a key outside the scope of a component or element.", ex.Message);
        }

        [Fact]
        public void CannotAddKeyOutsideComponentOrElement_RegionRoot()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());

            // Act/Assert
            builder.OpenElement(0, "some element");
            builder.OpenRegion(1);
            var ex = Assert.Throws<InvalidOperationException>(() =>
            {
                builder.SetKey(new object());
            });
            Assert.Equal($"Cannot set a key on a frame of type {RenderTreeFrameType.Region}.", ex.Message);
        }

        [Fact]
        public void IgnoresNullElementKey()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());

            // Act
            builder.OpenElement(0, "elem");
            builder.SetKey(null);
            builder.CloseElement();

            // Assert
            Assert.Collection(
                builder.GetFrames().AsEnumerable(),
                frame =>
                {
                    AssertFrame.Element(frame, "elem", 1, 0);
                    Assert.Null(frame.ElementKey);
                });
        }

        [Fact]
        public void IgnoresNullComponentKey()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());

            // Act
            builder.OpenComponent<TestComponent>(0);
            builder.SetKey(null);
            builder.CloseComponent();

            // Assert
            Assert.Collection(
                builder.GetFrames().AsEnumerable(),
                frame =>
                {
                    AssertFrame.Component<TestComponent>(frame, 1, 0);
                    Assert.Null(frame.ComponentKey);
                });
        }

        [Fact]
        public void ProcessDuplicateAttributes_DoesNotRemoveDuplicatesWithoutAddMultipleAttributes()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());
            builder.OpenElement(0, "div");
            builder.AddAttribute(0, "id", "hi");
            builder.AddAttribute(0, "id", "bye");
            builder.CloseElement();

            // Act
            var frames = builder.GetFrames().AsEnumerable();

            // Assert
            Assert.Collection(
                frames,
                f => AssertFrame.Element(f, "div", 3, 0),
                f => AssertFrame.Attribute(f, "id", "hi"),
                f => AssertFrame.Attribute(f, "id", "bye"));
        }


        [Fact]
        public void ProcessDuplicateAttributes_StopsAtFirstNonAttributeFrame_Capture()
        {
            // Arrange
            var capture = (Action<ElementRef>)((_) => { });

            var builder = new RenderTreeBuilder(new TestRenderer());
            builder.OpenElement(0, "div");
            builder.AddAttribute(0, "id", "hi");
            builder.AddMultipleAttributes(0, new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                { "id", "bye" },
            });
            builder.AddElementReferenceCapture(0, capture);
            builder.CloseElement();

            // Act
            var frames = builder.GetFrames().AsEnumerable();

            // Assert
            Assert.Collection(
                frames,
                f => AssertFrame.Element(f, "div", 3, 0),
                f => AssertFrame.Attribute(f, "id", "bye"),
                f => AssertFrame.ElementReferenceCapture(f, capture));
        }

        [Fact]
        public void ProcessDuplicateAttributes_StopsAtFirstNonAttributeFrame_Content()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());
            builder.OpenElement(0, "div");
            builder.AddAttribute(0, "id", "hi");
            builder.AddMultipleAttributes(0, new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                { "id", "bye" },
            });
            builder.AddContent(0, "hey");
            builder.CloseElement();

            // Act
            var frames = builder.GetFrames().AsEnumerable();

            // Assert
            Assert.Collection(
                frames,
                f => AssertFrame.Element(f, "div", 3, 0),
                f => AssertFrame.Attribute(f, "id", "bye"),
                f => AssertFrame.Text(f, "hey"));
        }

        [Fact]
        public void ProcessDuplicateAttributes_CanRemoveDuplicateInsideElement()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());
            builder.OpenElement(0, "div");
            builder.AddAttribute(0, "id", "hi");
            builder.AddMultipleAttributes(0, new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                { "id", "bye" },
            });
            builder.CloseElement();

            // Act
            var frames = builder.GetFrames().AsEnumerable();

            // Assert
            Assert.Collection(
                frames,
                f => AssertFrame.Element(f, "div", 2, 0),
                f => AssertFrame.Attribute(f, "id", "bye"));
        }

        [Fact]
        public void ProcessDuplicateAttributes_CanRemoveDuplicateInsideComponent()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());
            builder.OpenComponent<TestComponent>(0);
            builder.AddAttribute(0, "id", "hi");
            builder.AddMultipleAttributes(0, new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                { "id", "bye" },
            });
            builder.CloseComponent();

            // Act
            var frames = builder.GetFrames().AsEnumerable();

            // Assert
            Assert.Collection(
                frames,
                f => AssertFrame.Component<TestComponent>(f, 2, 0),
                f => AssertFrame.Attribute(f, "id", "bye"));
        }

        // This covers a special case we have to handle explicitly in the RTB logic.
        [Fact]
        public void ProcessDuplicateAttributes_SilentFrameFollowedBySameAttribute()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());
            builder.OpenComponent<TestComponent>(0);
            builder.AddAttribute(0, "id", (string)null);
            builder.AddMultipleAttributes(0, new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                { "id", "bye" },
            });
            builder.CloseComponent();

            // Act
            var frames = builder.GetFrames().AsEnumerable();

            // Assert
            Assert.Collection(
                frames,
                f => AssertFrame.Component<TestComponent>(f, 2, 0),
                f => AssertFrame.Attribute(f, "id", "bye"));
        }

        [Fact]
        public void ProcessDuplicateAttributes_DoesNotRemoveDuplicatesInsideChildElement()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());
            builder.OpenElement(0, "div");
            builder.AddAttribute(0, "id", "hi");
            builder.AddMultipleAttributes(0, new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                { "id", "bye" },
            });
            builder.OpenElement(0, "strong");
            builder.AddAttribute(0, "id", "hi");
            builder.AddAttribute(0, "id", "bye");
            builder.CloseElement();
            builder.CloseElement();

            // Act
            var frames = builder.GetFrames().AsEnumerable();

            // Assert
            Assert.Collection(
                frames,
                f => AssertFrame.Element(f, "div", 5, 0),
                f => AssertFrame.Attribute(f, "id", "bye"),
                f => AssertFrame.Element(f, "strong", 3),
                f => AssertFrame.Attribute(f, "id", "hi"),
                f => AssertFrame.Attribute(f, "id", "bye"));
        }

        [Fact]
        public void ProcessDuplicateAttributes_CanRemoveOverwrittenAttributes()
        {
            // Arrange
            var builder = new RenderTreeBuilder(new TestRenderer());
            builder.OpenElement(0, "div");
            builder.AddAttribute(0, "A", "hi");
            builder.AddAttribute(0, "2", new EventCallback(null, (Action)(() => { })));
            builder.AddMultipleAttributes(0, new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                { "a", null }, // Replace with null value (case-insensitive)
                { "2", false }, // Replace with 'false'
                { "3", "hey there" }, // Add a new value
            });
            builder.AddAttribute(0, "3", "see ya"); // Overwrite value added by splat
            builder.AddAttribute(0, "4", false); // Add a false value
            builder.AddAttribute(0, "5", "another one");
            builder.AddMultipleAttributes(0, new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                { "5", null }, // overwrite value with null
                { "6", new EventCallback(null, (Action)(() =>{ })) },
            });
            builder.AddAttribute(0, "6", default(EventCallback<string>)); // Replace with a 'silent' EventCallback<string>
            builder.CloseElement();

            // Act
            var frames = builder.GetFrames().AsEnumerable();

            // Assert
            Assert.Collection(
                frames,
                f => AssertFrame.Element(f, "div", 2, 0),
                f => AssertFrame.Attribute(f, "3", "see ya"));
        }

        private class TestComponent : IComponent
        {
            public void Configure(RenderHandle renderHandle) { }

            public Task SetParametersAsync(ParameterCollection parameters)
                => throw new NotImplementedException();
        }

        private class TestRenderer : Renderer
        {
            public TestRenderer() : base(new TestServiceProvider(), NullLoggerFactory.Instance)
            {
            }

            public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();

            protected override void HandleException(Exception exception)
                => throw new NotImplementedException();

            protected override Task UpdateDisplayAsync(in RenderBatch renderBatch)
                => throw new NotImplementedException();
        }
    }
}
