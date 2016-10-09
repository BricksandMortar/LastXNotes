using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Web.UI.WebControls;

using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;
using Rock.Web.UI.Controls;

namespace Rock.Reporting.DataSelect.Person
{
    /// <summary>
    /// 
    /// </summary>
    [Description( "Selects a Specific Number of Public Notes's Authors for a Person" )]
    [Export( typeof( DataSelectComponent ) )]
    [ExportMetadata( "ComponentName", "Selects a Specific Number of Public Notes's Authors for a Person" )]
    public class LastXNotesAuthorsSelect : DataSelectComponent
    {

        #region Properties

        /// <summary>
        /// Gets the name of the entity type. Filter should be an empty string
        /// if it applies to all entities
        /// </summary>
        /// <value>
        /// The name of the entity type.
        /// </value>
        public override string AppliesToEntityType
        {
            get
            {
                return typeof( Rock.Model.Person ).FullName;
            }
        }

        /// <summary>
        /// The PropertyName of the property in the anonymous class returned by the SelectExpression
        /// </summary>
        /// <value>
        /// The name of the column property.
        /// </value>
        public override string ColumnPropertyName
        {
            get
            {
                return "Last Notes' Authors";
            }
        }

        /// <summary>
        /// Gets the type of the column field.
        /// </summary>
        /// <value>
        /// The type of the column field.
        /// </value>
        public override Type ColumnFieldType
        {
            get { return typeof( IEnumerable<string> ); }
        }

        /// <summary>
        /// Gets the default column header text.
        /// </summary>
        /// <value>
        /// The default column header text.
        /// </value>
        public override string ColumnHeaderText
        {
            get
            {
                return "Last x Notes' Authors";
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Gets the title.
        /// </summary>
        /// <param name="entityType"></param>
        /// <returns></returns>
        /// <value>
        /// The title.
        /// </value>
        public override string GetTitle( Type entityType )
        {
            return "Last x Notes' Author";
        }

        /// <summary>
        /// Gets the expression.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="entityIdProperty">The entity identifier property.</param>
        /// <param name="selection">The selection.</param>
        /// <returns></returns>
        public override Expression GetExpression( RockContext context, MemberExpression entityIdProperty, string selection )
        {
            string[] selectionValues = selection.Split( '|' );
            int? noteTypeId = null;
            int numberOfNotes = 1;
            if ( selectionValues.Count() > 0 )
            {
                noteTypeId = selectionValues[0].AsIntegerOrNull();
                numberOfNotes = selectionValues[1].AsIntegerOrNull() ?? 1;
            }

            var entityTypeIdPerson = EntityTypeCache.GetId<Rock.Model.Person>();

            // only get PersonNotes that are not Private
            var qryNotes = new NoteService( context ).Queryable().Where( a => a.NoteType.EntityTypeId == entityTypeIdPerson && a.IsPrivateNote == false );

            if ( noteTypeId.HasValue )
            {
                qryNotes = qryNotes.Where( n => n.NoteTypeId == noteTypeId.Value );
            }

            //var personAliases = new PersonAliasService(context).Queryable()
            //    .Where( pa => qryNotes.Any( q => q.CreatedByPersonAliasId == pa.Id) );

            //var noteQuery = new PersonService( context ).Queryable()
            //    .Select( p => personAliases.Where( s => s.PersonId == p.Id && s.GroupRole.Guid == childGuid )
            //        .SelectMany( m => m.Group.Members )
            //        .Where( m => m.GroupRole.Guid == adultGuid )
            //        .Select( m => m.Person )
            //        .Where( m => m.PhoneNumbers.Count( t => t.NumberTypeValueId == phoneNumberTypeValueId ) != 0 )
            //        .Select( m => m.PhoneNumbers.FirstOrDefault( t => t.NumberTypeValueId == phoneNumberTypeValueId ) ).AsEnumerable() );


            var qryPersonNotes = new PersonService( context ).Queryable().
                Select( p => qryNotes
                    .Where( note => note.EntityId == p.Id )
                    .OrderByDescending( o => o.CreatedDateTime )
                    .Select( s => s.CreatedByPersonAlias )
                    .Select( pa => pa.Person )
                    .Select( person => person.NickName + " " + person.LastName )
                    .Take( numberOfNotes )
                    .AsEnumerable()
                );

            var selectNoteExpression = SelectExpressionExtractor.Extract( qryPersonNotes, entityIdProperty, "p" );

            return selectNoteExpression;
        }

        public override System.Web.UI.WebControls.DataControlField GetGridField( Type entityType, string selection )
        {
            var callbackField = new CallbackField();
            callbackField.OnFormatDataValue += ( sender, e ) =>
            {
                var noteList = e.DataValue as IEnumerable<string>;
                if ( noteList != null )
                {
                    var sb = new StringBuilder();
                    for (int i = 0; i < noteList.Count(); i++ )
                    {
                        sb.Append( "<strong>Note ");
                        sb.Append( i + 1 );
                        sb.Append( " Author:</strong> " );
                        sb.Append( noteList.ElementAt( i ));
                        if (i < noteList.Count() - 1 )
                        {
                            sb.Append( ", " );
                        }
                    }

                    e.FormattedValue = sb.ToString();
                }
                else
                {
                    e.FormattedValue = string.Empty;
                }
            };

            return callbackField;
        }

        /// <summary>
        /// Creates the child controls.
        /// </summary>
        /// <param name="parentControl"></param>
        /// <returns></returns>
        public override System.Web.UI.Control[] CreateChildControls( System.Web.UI.Control parentControl )
        {
            RockDropDownList ddlNoteType = new RockDropDownList();
            ddlNoteType.ID = parentControl.ID + "_ddlNoteType";
            ddlNoteType.Label = "Note Type";
            parentControl.Controls.Add( ddlNoteType );

            var noteTypeService = new NoteTypeService( new RockContext() );
            var noteTypes = noteTypeService.Queryable().OrderBy( a => a.Order ).ThenBy( a => a.Name ).Select( a => new
            {
                a.Id,
                a.Name
            } ).ToList();

            ddlNoteType.Items.Clear();
            ddlNoteType.Items.Add( new ListItem() );
            ddlNoteType.Items.AddRange( noteTypes.Select( a => new ListItem( a.Name, a.Id.ToString() ) ).ToArray() );

            NumberBox nbNumber = new NumberBox();
            nbNumber.ID = parentControl.ID + "_nbNumber";
            nbNumber.Label = "Number of Notes";
            nbNumber.MaximumValue = 10.ToString();
            nbNumber.MinimumValue = 1.ToString();
            parentControl.Controls.Add( nbNumber );

            return new System.Web.UI.Control[] { ddlNoteType, nbNumber };
        }

        /// <summary>
        /// Renders the controls.
        /// </summary>  
        /// <param name="parentControl">The parent control.</param>
        /// <param name="writer">The writer.</param>
        /// <param name="controls">The controls.</param>
        public override void RenderControls( System.Web.UI.Control parentControl, System.Web.UI.HtmlTextWriter writer, System.Web.UI.Control[] controls )
        {
            base.RenderControls( parentControl, writer, controls );
        }

        /// <summary>
        /// Gets the selection.
        /// </summary>
        /// <param name="controls">The controls.</param>
        /// <returns></returns>
        public override string GetSelection( System.Web.UI.Control[] controls )
        {
            if ( controls.Count() >= 0 )
            {
                RockDropDownList ddlNoteType = controls[0] as RockDropDownList;
                NumberBox nbNumber = controls[1] as NumberBox;
                return string.Format( "{0}|{1}", ddlNoteType.SelectedValue, nbNumber.Text );
            }

            return string.Empty;
        }

        /// <summary>
        /// Sets the selection.
        /// </summary>
        /// <param name="controls">The controls.</param>
        /// <param name="selection">The selection.</param>
        public override void SetSelection( System.Web.UI.Control[] controls, string selection )
        {
            if ( controls.Count() >= 0 )
            {
                string[] selectionValues = selection.Split( '|' );
                if ( selectionValues.Length >= 2 )
                {
                    RockDropDownList ddlNoteType = controls[0] as RockDropDownList;
                    ddlNoteType.SelectedValue = selectionValues[0];

                    NumberBox nbNumber = controls[1] as NumberBox;
                    nbNumber.Text = selectionValues[1];
                }
            }
        }


        #endregion

        internal class NoteGroup{
            public int NotePersonId { get; set; }
            public int NoteAuthorPersonId { get; set;}
            public int? NoteAuthorPersonAliasId { get; set; }
            public string NoteAuthorPersonName { get; set; }
            public DateTime? NoteCreatedDateTime { get; set; }
        }
    }
}
