﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using NPlant.Core;
using NPlant.Generation.ClassDiagraming;

namespace NPlant.MetaModel.ClassDiagraming
{
    public abstract class ClassDescriptor  : IKeyedItem
    {
        protected internal readonly IDictionary<string, bool> MemberVisibility = new Dictionary<string, bool>();
        private readonly KeyedList<ClassMemberDescriptor> _members = new KeyedList<ClassMemberDescriptor>();

        protected ClassDescriptor(Type reflectedType)
        {
            this.RenderInheritance = true;
            this.ReflectedType = reflectedType;
            this.Name = this.ReflectedType.Name;
        }

        public void Visit(ClassDiagramVisitorContext context)
        {
            this.MetaModel = context.GetTypeMetaModel(this.ReflectedType);

            LoadMembers(context);

            bool showInheritance = this.RenderInheritance && this.ReflectedType.BaseType != null;

            if (showInheritance)
            {
                var baseTypeMetaModel = context.GetTypeMetaModel(this.ReflectedType.BaseType);

                showInheritance = !baseTypeMetaModel.HideAsBaseClass && !baseTypeMetaModel.Hidden;
            }

            if (!this.MetaModel.Hidden)
            {
                foreach (ClassMemberDescriptor member in this.Members.InnerList)
                {
                    TypeMetaModel metaModel = member.MetaModel;

                    if (!metaModel.Hidden)
                    {
                        // if not showing inheritance then show all members
                        // otherwise, only show member that aren't inherited
                        if (!showInheritance || !member.IsInherited)
                        {
                            if (metaModel.IsComplexType && this.GetMemberVisibility(member.Key))
                            {
                                var nextLevel = this.Level + 1;

                                if (member.MemberType.IsEnumerable())
                                {
                                    var enumeratorType = member.MemberType.GetEnumeratorType();
                                    var enumeratorTypeMetaModel = context.GetTypeMetaModel(enumeratorType);

                                    if (enumeratorTypeMetaModel.IsComplexType)
                                    {
                                        context.AddRelatedClass(this, new ReflectedClassDescriptor(enumeratorType), ClassDiagramRelationshipTypes.HasMany, nextLevel, member.Name);
                                    }
                                }
                                else
                                {
                                    context.AddRelatedClass(this, new ReflectedClassDescriptor(member.MemberType), ClassDiagramRelationshipTypes.HasA, nextLevel, member.Name);
                                }
                            }
                        }
                    }
                }
            }

            if (showInheritance)
            {
                context.AddRelatedClass(this, new ReflectedClassDescriptor(this.ReflectedType.BaseType), ClassDiagramRelationshipTypes.Base, this.Level - 1);
            }
        }

        private void LoadMembers(ClassDiagramVisitorContext context)
        {
            switch (context.ScanMode)
            {
                case ClassDiagramScanModes.SystemServiceModelMember:
                    _members.AddRange(this.ReflectedType.GetFields()
                                                        .Where(x => x.HasAttribute<DataMemberAttribute>() || x.HasAttribute<MessageBodyMemberAttribute>())
                                                        .Select(field => new ClassMemberDescriptor(this.ReflectedType, field, context.GetTypeMetaModel(field.FieldType)))
                                     );
                    _members.AddRange(this.ReflectedType.GetProperties()
                                                        .Where(x => x.HasAttribute<DataMemberAttribute>() || x.HasAttribute<MessageBodyMemberAttribute>())
                                                        .Select(property => new ClassMemberDescriptor(this.ReflectedType, property, context.GetTypeMetaModel(property.PropertyType)))
                                     );
                    break;
                default:
                    _members.AddRange(this.ReflectedType.GetFields()
                                                        .Select(field => new ClassMemberDescriptor(this.ReflectedType, field, context.GetTypeMetaModel(field.FieldType)))
                                     );
                    _members.AddRange(this.ReflectedType.GetProperties()
                                                        .Select(property => new ClassMemberDescriptor(this.ReflectedType, property, context.GetTypeMetaModel(property.PropertyType)))
                                     );
                    break;
            }
        }

        string IKeyedItem.Key { get { return this.Name; } }

        public string Name { get; protected set; }

        public bool RenderInheritance { get; set; }

        public Type ReflectedType { get; private set; }

        public int Level { get; protected set; }
        
        public KeyedList<ClassMemberDescriptor> Members { get { return _members; }}

        public virtual bool GetMemberVisibility(string name)
        {
            bool visibility;

            if (MemberVisibility.TryGetValue(name, out visibility))
                return visibility;

            // default to visible (i.e. if no specification is present, assume visible)
            return true;
        }

        public TypeMetaModel MetaModel { get; private set; }

        public string Color { get; private set; }

        public override int GetHashCode()
        {
            return this.ReflectedType.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            ClassDescriptor descriptor = obj as ClassDescriptor;

            if (descriptor == null)
                return false;

            return descriptor.ReflectedType == this.ReflectedType;
        }
    }
}