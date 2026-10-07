/*
 * Local runtime for the PurrCare *.dc.html design-canvas mockups.
 *
 * Reimplements just enough of the hosted canvas's rendering engine to make these files open
 * directly in a browser (double-click, or file:// / a static server — either works):
 *   - {{expr}} interpolation in text nodes and attribute values
 *   - <sc-for list="{{expr}}" as="name"> loops
 *   - <sc-if value="{{expr}}"> conditionals
 *   - boolean attribute binding (checked/disabled/selected/…)
 *   - onClick/onChange/… event wiring to functions returned from renderVals()
 *   - DCLogic base class with state/setState driving a full re-render
 *
 * Screens whose renderVals() returns {} (the majority — see each area's README, "No live
 * interaction") render as static markup. A handful of files (TemplateEditor.dc.html,
 * TemplatePreviewStates.dc.html) show literal "{{catName}}"-style text on purpose — per
 * ADR-FE-023, the client never actually renders a template — so any {{expr}} whose identifier
 * isn't present in scope is left exactly as written rather than blanked out.
 *
 * Two layers are applied at render time on top of that, without touching the .dc.html source:
 *
 * 1. One staff shell. The four areas were designed with four different staff shells (Cat
 *    Profile's list screens: sidebar with the user bottom-left; its detail screens: no sidebar,
 *    user top-right; Administration: top bar over a flat sidebar; Accounts & Notifications: top
 *    bar only). Every staff screen is rebuilt into the same shape: a collapsible sidebar with one
 *    shared navigation on the left, and a header with the breadcrumb on the left and the short
 *    "Name · Role" (plus the branch picker, where the screen has one) top-right. Customer-portal
 *    screens are a different app and are left as designed.
 *
 * 2. Fluid layout. Each artboard was a fixed-size canvas frame (e.g. 1280x900); it now fills the
 *    viewport, fixed-width elements shrink instead of overflowing, the shell stacks below 900px,
 *    and wide grids/tables scroll horizontally instead of clipping.
 */
(function () {
  'use strict';

  // The artboards were authored for a canvas and have no viewport meta, so a phone would lay them
  // out at ~980px and shrink them. This script runs in <head>, early enough for the tag to apply.
  if (!document.querySelector('meta[name="viewport"]')) {
    var vp = document.createElement('meta');
    vp.name = 'viewport';
    vp.content = 'width=device-width, initial-scale=1';
    document.head.appendChild(vp);
  }

  var DC_UNRESOLVED = { unresolved: true };

  class DCLogic {
    constructor(props) {
      this.props = props || {};
      this.state = {};
    }
    setState(patch) {
      var next = typeof patch === 'function' ? patch(this.state) : patch;
      for (var k in next) if (Object.prototype.hasOwnProperty.call(next, k)) this.state[k] = next[k];
      if (this._render) this._render();
    }
    renderVals() {
      return {};
    }
  }
  window.DCLogic = DCLogic;

  function evalExpr(expr, scope) {
    var trimmed = expr.trim();
    if (trimmed === 'yes' || trimmed === 'true') return true;
    if (trimmed === 'no' || trimmed === 'false') return false;
    try {
      var fn = new Function('scope', 'with(scope){ return (' + trimmed + '); }');
      return fn(scope);
    } catch (e) {
      if (!(e instanceof ReferenceError)) {
        console.error('dc-support: expression error in "' + trimmed + '"', e);
      }
      return DC_UNRESOLVED;
    }
  }

  function soleExpr(str) {
    var m = /^\{\{([^}]+)\}\}$/.exec(str.trim());
    return m ? m[1] : null;
  }

  function interpolateString(str, scope) {
    return str.replace(/\{\{([^}]+)\}\}/g, function (whole, expr) {
      var v = evalExpr(expr, scope);
      if (v === DC_UNRESOLVED) return whole;
      return v === undefined || v === null ? '' : String(v);
    });
  }

  var BOOL_ATTRS = {
    checked: 1, disabled: 1, selected: 1, readonly: 1, required: 1, hidden: 1, multiple: 1, open: 1
  };

  function processAttributes(node, scope) {
    Array.prototype.slice.call(node.attributes).forEach(function (attr) {
      var name = attr.name;
      var value = attr.value;

      if (/^on[a-z]+$/.test(name)) {
        var evExpr = soleExpr(value);
        if (evExpr !== null) {
          node.removeAttribute(name);
          var fn = evalExpr(evExpr, scope);
          if (typeof fn === 'function') {
            node.addEventListener(name.slice(2), function (e) { fn(e); });
          }
        }
        return;
      }

      if (value.indexOf('{{') === -1) return;

      if (BOOL_ATTRS[name]) {
        var bExpr = soleExpr(value);
        if (bExpr !== null) {
          var bVal = evalExpr(bExpr, scope);
          if (bVal === DC_UNRESOLVED) return;
          if (bVal) {
            node.setAttribute(name, '');
            if (name === 'checked') node.checked = true;
            if (name === 'selected') node.selected = true;
          } else {
            node.removeAttribute(name);
            if (name === 'checked') node.checked = false;
            if (name === 'selected') node.selected = false;
          }
          return;
        }
      }

      var interpolated = interpolateString(value, scope);
      node.setAttribute(name, interpolated);
      if (name === 'value' && (node.tagName === 'INPUT' || node.tagName === 'TEXTAREA')) {
        node.value = interpolated;
      }
    });
  }

  function processChildren(parent, scope) {
    Array.prototype.slice.call(parent.childNodes).forEach(function (child) {
      processNode(parent, child, scope);
    });
  }

  function cloneChildrenInto(fragmentTarget, templateNode, scope) {
    Array.prototype.slice.call(templateNode.childNodes).forEach(function (c) {
      fragmentTarget.appendChild(c.cloneNode(true));
    });
    processChildren(fragmentTarget, scope);
  }

  function processNode(parent, node, scope) {
    if (node.nodeType === Node.TEXT_NODE) {
      if (node.nodeValue.indexOf('{{') !== -1) {
        node.nodeValue = interpolateString(node.nodeValue, scope);
      }
      return;
    }
    if (node.nodeType !== Node.ELEMENT_NODE) return;

    var tag = node.tagName.toLowerCase();

    if (tag === 'sc-for') {
      var listExpr = soleExpr(node.getAttribute('list') || '');
      var asName = node.getAttribute('as') || 'item';
      var list = listExpr !== null ? evalExpr(listExpr, scope) : [];
      if (list === DC_UNRESOLVED || !Array.isArray(list)) list = [];
      var acc = document.createDocumentFragment();
      list.forEach(function (item, idx) {
        var childScope = Object.create(scope);
        childScope[asName] = item;
        childScope.$index = idx;
        var wrapper = document.createDocumentFragment();
        cloneChildrenInto(wrapper, node, childScope);
        acc.appendChild(wrapper);
      });
      parent.replaceChild(acc, node);
      return;
    }

    if (tag === 'sc-if') {
      var condExpr = soleExpr(node.getAttribute('value') || '');
      var cond = condExpr !== null ? evalExpr(condExpr, scope) : false;
      if (cond === DC_UNRESOLVED) cond = false;
      if (cond) {
        var wrapper2 = document.createDocumentFragment();
        cloneChildrenInto(wrapper2, node, scope);
        parent.replaceChild(wrapper2, node);
      } else {
        parent.removeChild(node);
      }
      return;
    }

    processAttributes(node, scope);
    processChildren(node, scope);
  }

  /* ---------------------------------------------------------------- staff shell */

  var PATH_PARTS = decodeURIComponent(location.pathname).split('/');
  var PAGE = (PATH_PARTS[PATH_PARTS.length - 1] || '').replace(/\.dc\.html$/, '');
  var AREA = PATH_PARTS[PATH_PARTS.length - 2] || '';

  var I = {
    home: '<path d="M3 11l9-7 9 7"/><path d="M5 10v10h14V10"/>',
    chart: '<path d="M4 20V10M10 20V4M16 20v-7M22 20H2"/>',
    clipboard: '<rect x="5" y="4" width="14" height="17" rx="2"/><path d="M9 4V3h6v1M9 10h6M9 14h6"/>',
    calendar: '<rect x="3" y="5" width="18" height="16" rx="2"/><path d="M8 3v4M16 3v4M3 11h18"/>',
    list: '<path d="M4 6h16M4 12h16M4 18h16"/>',
    stethoscope: '<path d="M6 3v6a4 4 0 0 0 8 0V3"/><path d="M10 13v3a5 5 0 0 0 10 0v-2"/><circle cx="20" cy="12" r="2"/>',
    flask: '<path d="M9 3h6M10 3v6L4 19a1 1 0 0 0 .9 1.5h14.2A1 1 0 0 0 20 19L14 9V3"/><path d="M7 14h10"/>',
    scissors: '<circle cx="6" cy="6" r="3"/><circle cx="6" cy="18" r="3"/><path d="M20 4 8.1 15.9M14.5 14.5 20 20M8.1 8.1 12 12"/>',
    pulse: '<path d="M3 12h4l2-5 4 10 2-5h6"/>',
    pill: '<path d="m10.5 20.5 10-10a4.95 4.95 0 1 0-7-7l-10 10a4.95 4.95 0 1 0 7 7Z"/><path d="m8.5 8.5 7 7"/>',
    lock: '<rect x="5" y="11" width="14" height="10" rx="2"/><path d="M8 11V7a4 4 0 0 1 8 0v4"/>',
    syringe: '<path d="m18 2 4 4M17 7 7 17l-4 4M14 4l6 6M9 11l4 4"/>',
    house: '<path d="M3 9h18v11H3zM3 9l2-5h14l2 5M12 9v11"/>',
    checklist: '<path d="M10 6h11M10 12h11M10 18h11"/><path d="m3 6 1.5 1.5L7 5M3 12l1.5 1.5L7 11M3 18l1.5 1.5L7 17"/>',
    user: '<circle cx="10" cy="8" r="3.5"/><path d="M3 20v-1a5 5 0 0 1 5-5h4a5 5 0 0 1 5 5v1"/>',
    copy: '<rect x="8" y="8" width="12" height="12" rx="2"/><path d="M16 8V6a2 2 0 0 0-2-2H6a2 2 0 0 0-2 2v8a2 2 0 0 0 2 2h2"/>',
    repeat: '<path d="M17 2l4 4-4 4"/><path d="M3 11V9a3 3 0 0 1 3-3h15"/><path d="M7 22l-4-4 4-4"/><path d="M21 13v2a3 3 0 0 1-3 3H3"/>',
    bell: '<path d="M6 8a6 6 0 0 1 12 0c0 7 3 9 3 9H3s3-2 3-9"/><path d="M10.3 21a1.94 1.94 0 0 0 3.4 0"/>',
    bag: '<path d="M6 7h12l1 14H5z"/><path d="M9 7a3 3 0 0 1 6 0"/>',
    box: '<path d="M21 8 12 3 3 8v8l9 5 9-5z"/><path d="M3 8l9 5 9-5M12 13v8"/>',
    undo: '<path d="M9 14 4 9l5-5"/><path d="M4 9h11a5 5 0 0 1 0 10h-3"/>',
    register: '<rect x="3" y="4" width="18" height="12" rx="2"/><path d="M8 20h8M12 16v4"/>',
    receipt: '<path d="M5 3h14v18l-3-2-2 2-2-2-2 2-2-2-3 2z"/><path d="M9 8h6M9 12h6"/>',
    file: '<path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8Z"/><path d="M14 2v6h6"/><path d="M8 13h8M8 17h8"/>',
    cash: '<rect x="2" y="6" width="20" height="12" rx="2"/><circle cx="12" cy="12" r="2.5"/>',
    approve: '<circle cx="12" cy="12" r="9"/><path d="m8 12 3 3 5-6"/>',
    trend: '<path d="m3 7 6 6 4-4 8 8"/><path d="M21 11v6h-6"/>',
    coins: '<ellipse cx="12" cy="6" rx="8" ry="3"/><path d="M4 6v6c0 1.7 3.6 3 8 3s8-1.3 8-3V6M4 12v6c0 1.7 3.6 3 8 3s8-1.3 8-3v-6"/>',
    swap: '<path d="M4 7h16l-4-4M20 17H4l4 4"/>',
    download: '<path d="M12 3v12M7 10l5 5 5-5M4 21h16"/>',
    grid: '<rect x="3" y="3" width="8" height="8" rx="1"/><rect x="13" y="3" width="8" height="8" rx="1"/><rect x="3" y="13" width="8" height="8" rx="1"/><rect x="13" y="13" width="8" height="8" rx="1"/>',
    inbox: '<path d="M3 13h5l1 3h6l1-3h5"/><path d="M5 5h14l2 8v6H3v-6z"/>',
    updown: '<path d="M7 20V4M3 8l4-4 4 4M17 4v16M13 16l4 4 4-4"/>',
    alert: '<path d="M12 9v4M12 17h.01"/><path d="M10.3 3.9 2.6 17.2A2 2 0 0 0 4.3 20h15.4a2 2 0 0 0 1.7-2.8L13.7 3.9a2 2 0 0 0-3.4 0Z"/>',
    ban: '<circle cx="12" cy="12" r="9"/><path d="m6 6 12 12"/>',
    thermo: '<path d="M14 14.8V4a2 2 0 0 0-4 0v10.8a4 4 0 1 0 4 0Z"/>',
    truck: '<path d="M3 6h11v10H3zM14 10h4l3 3v3h-7"/><circle cx="7" cy="18" r="2"/><circle cx="17" cy="18" r="2"/>',
    container: '<path d="M3 17l2 4h14l2-4H3z"/><path d="M6 17V9h12v8M9 9V5h6v4"/>',
    refresh: '<path d="M21 12a9 9 0 1 1-3-6.7L21 8"/><path d="M21 3v5h-5"/>',
    pin: '<path d="M12 21s7-7.5 7-12a7 7 0 1 0-14 0c0 4.5 7 12 7 12Z"/><circle cx="12" cy="9" r="2.5"/>',
    clock: '<circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/>',
    users: '<circle cx="9" cy="8" r="3.5"/><path d="M2 20v-1a5 5 0 0 1 5-5h4a5 5 0 0 1 5 5v1"/><path d="M16 4.5a3.5 3.5 0 0 1 0 7M22 20v-1a5 5 0 0 0-4-4.9"/>',
    sliders: '<path d="M4 6h10M4 12h16M4 18h10"/><circle cx="17" cy="6" r="2"/><circle cx="9" cy="18" r="2"/>',
    shield: '<path d="M12 3l7 3v6c0 4.5-3 8-7 9-4-1-7-4.5-7-9V6Z"/><path d="m9 12 2 2 4-4"/>',
    mail: '<rect x="3" y="5" width="18" height="14" rx="2"/><path d="m3 7 9 6 9-6"/>',
    lines: '<path d="M9 6h11M9 12h11M9 18h11"/><path d="M4 6h.01M4 12h.01M4 18h.01"/>',
    power: '<path d="M12 3v9"/><path d="M6.4 6.4a8 8 0 1 0 11.2 0"/>',
    tag: '<path d="M3 12V5a2 2 0 0 1 2-2h7l9 9-9 9-9-9Z"/><circle cx="8.5" cy="8.5" r="1.5"/>',
    book: '<path d="M4 19V5a2 2 0 0 1 2-2h14v16H6a2 2 0 0 0-2 2z"/><path d="M8 7h8"/>',
    megaphone: '<path d="M3 11v2l13 5V6z"/><path d="M16 8a4 4 0 0 1 0 8"/>',
    map: '<path d="M9 4 3 6v14l6-2 6 2 6-2V4l-6 2z"/><path d="M9 4v14M15 6v14"/>',
    percent: '<path d="M19 5 5 19"/><circle cx="7" cy="7" r="2"/><circle cx="17" cy="17" r="2"/>',
    archive: '<rect x="3" y="4" width="18" height="5" rx="1"/><path d="M5 9v11h14V9M10 13h4"/>',
    database: '<ellipse cx="12" cy="5" rx="8" ry="3"/><path d="M4 5v14c0 1.7 3.6 3 8 3s8-1.3 8-3V5M4 12c0 1.7 3.6 3 8 3s8-1.3 8-3"/>',
    gauge: '<path d="M12 14l4-4"/><path d="M3.3 19a10 10 0 1 1 17.4 0"/>'
  };

  // One navigation for every staff screen in every area. `match` lists the screens that
  // highlight that item; hrefs are resolved relative to whichever area folder is being viewed.
  var STAFF_NAV = [
    { section: 'Overview' },
    { label: 'Home', area: 'reporting', file: 'DashboardReception.dc.html', icon: I.home,
      match: ['DashboardReception', 'DashboardAdmin', 'DashboardNurse', 'DashboardStates', 'DashboardMobile'] },
    { label: 'Reports', area: 'reporting', file: 'ReportCatalogue.dc.html', icon: I.chart,
      match: ['ReportCatalogue', 'ReportViewer', 'ReportRevenueChart', 'ReportRestrictedColumns', 'ReportFilterBar',
        'ReportFilterErrors', 'ReportEmptyLoading', 'ReportExportStates', 'ReportMobile', 'ReportPermissionDenied'] },

    { section: 'Clinic' },
    { label: 'Front desk', area: 'appointments', file: 'FrontDeskToday.dc.html', icon: I.clipboard,
      match: ['FrontDeskToday', 'FrontDeskQueue', 'FrontDeskStart', 'FrontDeskOffline', 'OfflineBannerStates'] },
    { label: 'Appointments', area: 'appointments', file: 'Calendar.dc.html', icon: I.calendar,
      match: ['Calendar', 'CalendarMoveConfirm', 'CalendarMoveRefused', 'MoveDialog', 'CalendarAgenda360',
        'StaffBooking', 'StaffAppointmentDetail', 'FlaggedAppointments'] },
    { label: 'Cats', area: 'cat-profile', file: 'Main.dc.html', icon: I.list,
      match: ['Main', 'CatOverview', 'CatHealthy', 'CatDeceased', 'CatMedical', 'CatWeights', 'CatVaccinations',
        'CatRegister', 'IdleLock', 'ClinicalHistoryPage', 'ConsentListAndDetail', 'ConsentCapture', 'ConsentStates',
        'CatPrescriptions', 'ComposerWarnings', 'ComposerAllergyBlock', 'ComposerWeightRequired', 'ComposerStates',
        'PrescriptionDetail', 'DietAuthorisationForm', 'CatDietAuthorisations'] },
    { label: 'Visits', area: 'consultation', file: 'OpenVisits.dc.html', icon: I.stethoscope,
      match: ['OpenVisits', 'QuickVitals', 'VitalsConfirm', 'Workspace', 'DraftRecovery', 'SaveConflict', 'ProblemList',
        'WorkspaceRoles', 'WorkspaceMobile360', 'FinaliseDialog', 'VisitRecord', 'HistoryDrawer', 'OrderDiagnostic',
        'RecordTreatment', 'TreatmentStates', 'PhotosAndFollowUp', 'DischargeSummary'] },
    { label: 'Diagnostics', area: 'consultation', file: 'PendingDiagnostics.dc.html', icon: I.flask,
      match: ['PendingDiagnostics', 'ResultEntry', 'DiagnosticOrderDetail'] },
    { label: 'Surgery', area: 'surgery', file: 'TheatreDayList.dc.html', icon: I.scissors,
      match: ['TheatreDayList', 'BookSurgery', 'BookSurgeryClashes', 'BookSurgeryStates', 'SurgeryPreOp', 'SurgeryPreOpVet',
        'ConsentOverride', 'RescheduleCancel', 'AnaesthesiaLog', 'DrugEntry', 'VitalsEntry', 'CorrectEntry', 'EndProcedure',
        'ReportDraft', 'ReportFinal', 'RecoveryTab', 'DischargeForm', 'Discharged'] },
    { label: 'Recovery board', area: 'surgery', file: 'RecoveryBoard.dc.html', icon: I.pulse, match: ['RecoveryBoard'] },
    { label: 'Pharmacy', area: 'pharmacy', file: 'DispensaryQueue.dc.html', icon: I.pill,
      match: ['DispensaryQueue', 'DispenseScreen', 'DispenseStates', 'WitnessDialog', 'DispensingDetail', 'MedicineLabel'] },
    { label: 'Controlled drugs', area: 'pharmacy', file: 'ControlledRegister.dc.html', icon: I.lock,
      match: ['ControlledRegister', 'RegisterBalancesIntegrity', 'RegisterEntryDetail', 'RegisterCorrection'] },
    { label: 'Vaccines', area: 'cat-profile', file: 'Vaccines.dc.html', icon: I.syringe, match: ['Vaccines'] },

    { section: 'Boarding' },
    { label: 'Boarding', area: 'boarding', file: 'BoardingDashboard.dc.html', icon: I.house,
      match: ['BoardingDashboard', 'StayList', 'StaffBookingOverride', 'StayWorkspace', 'CheckInVaccinationHold',
        'CheckInBelongingsSignature', 'CheckInIncomplete', 'CheckOutEarly', 'OccupancyBoard', 'ExtendRelocate',
        'IsolationPanel', 'ReferralCharges', 'IncidentQueue', 'IncidentFormResolve', 'DaycareDayList'] },
    { label: 'Care tasks', area: 'boarding', file: 'TaskBoard.dc.html', icon: I.checklist, match: ['TaskBoard', 'ObservationSheet'] },

    { section: 'Customers' },
    { label: 'Customers', area: 'accounts-identity', file: 'CustomerDetail.dc.html', icon: I.user,
      match: ['CustomerDetail', 'WalkIn', 'CustomerAppointmentsPanel', 'CustomerSubscriptionsPanel', 'CustomerOrdersPanel',
        'ErasePersonalData', 'EraseConfirm', 'EraseStates'] },
    { label: 'Duplicates', area: 'accounts-identity', file: 'Duplicates.dc.html', icon: I.copy, match: ['Duplicates', 'MergeReview'] },
    { label: 'Subscriptions', area: 'subscriptions', file: 'StaffSubscriptions.dc.html', icon: I.repeat,
      match: ['StaffSubscriptions', 'StaffUnpaid', 'StaffPrescriptionSuspended', 'StaffSubscriptionDetail', 'StaffDialogs',
        'StaffDetailReadOnly'] },
    { label: 'Notification log', area: 'notifications', file: 'NotificationLog.dc.html', icon: I.bell,
      match: ['NotificationLog', 'NotificationDetail'] },

    { section: 'Shop' },
    { label: 'Catalogue', area: 'shop', file: 'StaffProductList.dc.html', icon: I.bag,
      match: ['StaffProductList', 'NewProduct', 'EditorDetails', 'EditorFood', 'EditorVariants', 'EditorImages',
        'EditorBundle', 'EditorRelated', 'EditorTranslations', 'SourcingOrigins', 'ReviewModeration'] },
    { label: 'Orders', area: 'orders', file: 'OrderQueue.dc.html', icon: I.box,
      match: ['OrderQueue', 'OrderQueueMobile', 'StaffOrderDetail', 'DispatchDialog', 'RecordDelivery', 'RecordCollection',
        'DeliveryFailure', 'AwaitingPayment', 'PaymentApproval', 'Reconciliation', 'Refunds', 'PickList'] },
    { label: 'Returns', area: 'orders', file: 'ReturnsList.dc.html', icon: I.undo, match: ['ReturnsList', 'ReturnDetail', 'ReturnReceive'] },
    { label: 'Point of sale', area: 'orders', file: 'PosSale.dc.html', icon: I.register, match: ['PosSale', 'PosComplete', 'PosMobile'] },

    { section: 'Billing' },
    { label: 'Billing home', area: 'billing', file: 'BillingHome.dc.html', icon: I.receipt,
      match: ['BillingHome', 'CustomerAccount', 'ApplyCreditAndVoid'] },
    { label: 'Invoices', area: 'billing', file: 'InvoiceList.dc.html', icon: I.file,
      match: ['InvoiceList', 'InvoiceComposer', 'ComposerDialogs', 'ComposerErrors', 'InvoiceDetail', 'CreditNoteWizard',
        'CreditNoteResult', 'CreditNoteDetail', 'CorrectedReissue'] },
    { label: 'Payments', area: 'billing', file: 'PaymentList.dc.html', icon: I.cash,
      match: ['PaymentList', 'RecordPayment', 'PaymentMethodVariants', 'SplitTender', 'PaymentDetail'] },
    { label: 'Approvals', area: 'billing', file: 'ApprovalsQueue.dc.html', icon: I.approve, match: ['ApprovalsQueue'] },
    { label: 'Receivables', area: 'billing', file: 'Receivables.dc.html', icon: I.trend, match: ['Receivables'] },
    { label: 'Cash-up', area: 'billing', file: 'CashUp.dc.html', icon: I.coins, match: ['CashUp', 'CashUpClosed'] },
    { label: 'Reconciliation', area: 'billing', file: 'CodReconciliation.dc.html', icon: I.swap,
      match: ['CodReconciliation', 'BankStatements', 'BankMatch'] },
    { label: 'Accounting export', area: 'billing', file: 'AccountingExport.dc.html', icon: I.download, match: ['AccountingExport'] },

    { section: 'Inventory' },
    { label: 'Stock', area: 'imports-inventory', file: 'StockOverview.dc.html', icon: I.grid,
      match: ['StockOverview', 'SkuStockDetail', 'BatchDetail', 'Ledger'] },
    { label: 'Goods receipt', area: 'imports-inventory', file: 'GoodsReceipt.dc.html', icon: I.inbox,
      match: ['GoodsReceipt', 'GoodsReceiptErrors', 'GoodsReceiptDialogs', 'ReceiptDetail'] },
    { label: 'Stock control', area: 'imports-inventory', file: 'TransferForm.dc.html', icon: I.updown,
      match: ['TransferForm', 'AdjustmentForm', 'ApprovalQueue', 'CountSheet', 'CountVariance'] },
    { label: 'Alerts & reports', area: 'imports-inventory', file: 'Alerts.dc.html', icon: I.alert, match: ['Alerts', 'NearExpiryReport'] },
    { label: 'Recalls', area: 'imports-inventory', file: 'RecallList.dc.html', icon: I.ban,
      match: ['RecallList', 'DeclareRecall', 'RecallDetail', 'RecallDialogs'] },
    { label: 'Storage', area: 'imports-inventory', file: 'StorageBreach.dc.html', icon: I.thermo, match: ['StorageBreach', 'StockSettings'] },

    { section: 'Procurement' },
    { label: 'Suppliers', area: 'imports-inventory', file: 'Suppliers.dc.html', icon: I.truck, match: ['Suppliers'] },
    { label: 'Purchase orders', area: 'imports-inventory', file: 'PurchaseOrderList.dc.html', icon: I.clipboard,
      match: ['PurchaseOrderList', 'PurchaseOrderBuilder', 'PurchaseOrderDetail'] },
    { label: 'Consignments', area: 'imports-inventory', file: 'ConsignmentList.dc.html', icon: I.container,
      match: ['ConsignmentList', 'ConsignmentDetails', 'ConsignmentDocuments', 'ConsignmentCosts', 'InspectionPack'] },
    { label: 'Reorder', area: 'imports-inventory', file: 'ReorderSuggestions.dc.html', icon: I.refresh, match: ['ReorderSuggestions'] },

    { section: 'Configuration' },
    { label: 'Services & pricing', area: 'administration', file: 'ServiceList.dc.html', icon: I.tag,
      match: ['ServiceList', 'ServiceForm', 'EffectiveDatedHistory', 'TaxClasses'] },
    { label: 'Clinical catalogues', area: 'consultation', file: 'DiagnosisCodes.dc.html', icon: I.book,
      match: ['DiagnosisCodes', 'CatalogueEditors'] },
    { label: 'Formulary', area: 'pharmacy', file: 'FormularyList.dc.html', icon: I.pill,
      match: ['FormularyList', 'FormularyEditor', 'FormularyActions'] },
    { label: 'Surgery protocols', area: 'surgery', file: 'Protocols.dc.html', icon: I.checklist, match: ['Protocols'] },
    { label: 'Surgery campaigns', area: 'surgery', file: 'Campaigns.dc.html', icon: I.megaphone, match: ['Campaigns', 'CampaignCases'] },
    { label: 'Boarding units & rates', area: 'boarding', file: 'AdminUnits.dc.html', icon: I.house, match: ['AdminUnits', 'AdminRateCards'] },
    { label: 'Delivery zones', area: 'orders', file: 'DeliveryZones.dc.html', icon: I.map, match: ['DeliveryZones'] },
    { label: 'Discount codes', area: 'orders', file: 'DiscountCodes.dc.html', icon: I.percent, match: ['DiscountCodes'] },
    { label: 'Couriers', area: 'orders', file: 'Couriers.dc.html', icon: I.truck, match: ['Couriers'] },
    { label: 'Payment settings', area: 'orders', file: 'PaymentSettings.dc.html', icon: I.cash, match: ['PaymentSettings'] },
    { label: 'Billing profile', area: 'billing', file: 'BillingProfile.dc.html', icon: I.receipt, match: ['BillingProfile'] },
    { label: 'Message templates', area: 'notifications', file: 'TemplateList.dc.html', icon: I.mail,
      match: ['TemplateList', 'TemplateEditor', 'TemplatePreviewStates'] },
    { label: 'Content', area: 'administration', file: 'ContentList.dc.html', icon: I.file, match: ['ContentList', 'ContentEditor'] },
    { label: 'Policies', area: 'administration', file: 'PolicyList.dc.html', icon: I.shield, match: ['PolicyList'] },

    { section: 'Administration' },
    { label: 'Branches', area: 'administration', file: 'BranchList.dc.html', icon: I.pin,
      match: ['BranchList', 'BranchDetails', 'WeeklyHoursEditor', 'Closures', 'Rooms'] },
    { label: 'Rosters & leave', area: 'administration', file: 'RosterWeekView.dc.html', icon: I.clock,
      match: ['RosterWeekView', 'RosterPatternEditor', 'MyLeave', 'LeaveApprovals'] },
    { label: 'Staff accounts', area: 'accounts-identity', file: 'StaffAccountsList.dc.html', icon: I.users,
      match: ['StaffAccountsList', 'StaffAccountCreate', 'StaffAccountDetail'] },
    { label: 'Settings', area: 'administration', file: 'SettingsForm.dc.html', icon: I.sliders, match: ['SettingsForm', 'OverdueBannerOnSettings'] },
    { label: 'Report schedules', area: 'reporting', file: 'ScheduleList.dc.html', icon: I.calendar,
      match: ['ScheduleList', 'ScheduleForm', 'ScheduleDetail', 'ScheduleConfirmDialogs'] },
    { label: 'Retention', area: 'cross-cutting', file: 'RetentionSchedule.dc.html', icon: I.archive,
      match: ['RetentionSchedule', 'RetentionRunNow', 'RetentionRuns', 'RetentionRunDetail', 'ErasureLedger',
        'ErasureLedgerDetail', 'ReapplyAfterRestore', 'ReapplyStates'] },
    { label: 'Backup & restore', area: 'cross-cutting', file: 'BackupStatus.dc.html', icon: I.database,
      match: ['BackupStatus', 'RecordRestoreTest', 'RecordRestoreTestParseError', 'RestoreTestDetail'] },
    { label: 'Performance', area: 'cross-cutting', file: 'Performance.dc.html', icon: I.gauge, match: ['Performance'] },
    { label: 'Audit log', area: 'administration', file: 'AuditLog.dc.html', icon: I.lines, match: ['AuditLog'] },
    { label: 'System', area: 'administration', file: 'MaintenanceSwitch.dc.html', icon: I.power, match: ['MaintenanceSwitch'] }
  ];

  // Cat Profile was designed before sign-in existed and shows a fixed identity throughout.
  var CAT_PROFILE_WHO = { name: 'Dr. A. Rahman', role: 'Veterinarian', note: 'Fixed dev identity — no sign-in yet' };

  var BRAND_SVG = '<svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="#15605A" stroke-width="1.6" ' +
    'stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M12 5c-3.3 0-6 2.5-6 5.6 0 2.2 1.2 3.4 1.2 5.2 ' +
    '0 1.2-.6 1.7-.6 2.4 0 .6.5 1 1.2 1h8.4c.7 0 1.2-.4 1.2-1 0-.7-.6-1.2-.6-2.4 0-1.8 1.2-3 1.2-5.2C18 7.5 15.3 5 12 5Z"/>' +
    '<path d="M7 7 5.5 4.2 8.4 5.1"/><path d="m17 7 1.5-2.8-2.9.9"/></svg>';

  function navHref(area, file) {
    return area === AREA ? file : '../' + area + '/' + file;
  }

  function iconSvg(inner) {
    return '<svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" ' +
      'stroke-linecap="round" stroke-linejoin="round" aria-hidden="true" style="flex-shrink:0">' + inner + '</svg>';
  }

  function escapeHtml(s) {
    return String(s).replace(/[&<>"']/g, function (c) {
      return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
    });
  }

  // Returns { item, section } for the current screen, or {} when it has no nav entry.
  function activeNav() {
    var section = '';
    for (var i = 0; i < STAFF_NAV.length; i++) {
      var n = STAFF_NAV[i];
      if (n.section) { section = n.section; continue; }
      if (n.match && n.match.indexOf(PAGE) !== -1) return { item: n, section: section };
    }
    return {};
  }

  function buildAside(active) {
    var aside = document.createElement('aside');
    aside.className = 'dc-aside';
    aside.style.cssText = 'width:240px;flex-shrink:0;align-self:flex-start;position:sticky;top:0;height:100vh;' +
      'overflow-y:auto;background:#F3F1EB;border-right:1px solid #E5E1D8;padding:20px 14px;' +
      'display:flex;flex-direction:column;gap:18px;box-sizing:border-box;';

    var row = document.createElement('div');
    row.className = 'dc-nav-toggle-row';
    row.style.cssText = 'display:flex;align-items:center;justify-content:space-between;gap:6px;';
    var brand = document.createElement('a');
    brand.href = navHref('cat-profile', 'Main.dc.html');
    brand.style.cssText = 'display:flex;align-items:center;gap:10px;text-decoration:none;color:#1C1A17;min-width:0;';
    brand.innerHTML = BRAND_SVG + '<span class="dc-nav-label" style="font-family:\'Newsreader\',Georgia,serif;' +
      'font-size:20px;font-weight:600;letter-spacing:-0.01em;white-space:nowrap;">PurrCare</span>';
    var btn = document.createElement('button');
    btn.type = 'button';
    btn.className = 'dc-nav-toggle';
    btn.style.cssText = 'border:1px solid #DDD8CD;background:#fff;border-radius:6px;width:26px;height:26px;' +
      'display:flex;align-items:center;justify-content:center;cursor:pointer;flex-shrink:0;padding:0;';
    btn.innerHTML = '<svg class="dc-nav-toggle-icon" width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="#5C574F" ' +
      'stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M15 18l-6-6 6-6"/></svg>' +
      '<svg class="dc-nav-menu-icon" width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="#5C574F" ' +
      'stroke-width="2.2" stroke-linecap="round" aria-hidden="true"><path d="M4 6h16M4 12h16M4 18h16"/></svg>';
    row.appendChild(brand);
    row.appendChild(btn);
    aside.appendChild(row);

    var nav = document.createElement('nav');
    nav.setAttribute('aria-label', 'Main');
    nav.style.cssText = 'display:flex;flex-direction:column;gap:2px;';

    // Sections fold: the one holding the current page is always open, the rest remember
    // whether you last left them open. Keeps a ~70-item menu scannable.
    var OPEN_KEY = 'purrcare-nav-open';
    var openSections = [];
    try { openSections = JSON.parse(window.localStorage.getItem(OPEN_KEY) || '[]'); } catch (e) { openSections = []; }
    function saveOpen() {
      try { window.localStorage.setItem(OPEN_KEY, JSON.stringify(openSections)); } catch (e) { /* ignore */ }
    }

    var group = null, items = null;
    STAFF_NAV.forEach(function (n) {
      if (n.section) {
        group = document.createElement('div');
        group.className = 'dc-nav-group';
        var isOpen = n.section === active.section || openSections.indexOf(n.section) !== -1;
        if (!isOpen) group.classList.add('dc-closed');
        var head = document.createElement('button');
        head.type = 'button';
        head.className = 'dc-nav-label dc-nav-section';
        head.setAttribute('aria-expanded', String(isOpen));
        head.style.cssText = 'display:flex;align-items:center;justify-content:space-between;width:100%;border:none;' +
          'background:none;cursor:pointer;font:inherit;font-size:11px;font-weight:700;letter-spacing:.06em;' +
          'text-transform:uppercase;color:#8C8578;padding:10px 12px 4px;text-align:left;';
        head.innerHTML = '<span>' + escapeHtml(n.section) + '</span><svg class="dc-nav-chevron" width="12" height="12" ' +
          'viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" ' +
          'stroke-linejoin="round" aria-hidden="true"><path d="m6 9 6 6 6-6"/></svg>';
        (function (g, h, name) {
          h.addEventListener('click', function () {
            var nowOpen = g.classList.contains('dc-closed');
            g.classList.toggle('dc-closed', !nowOpen);
            h.setAttribute('aria-expanded', String(nowOpen));
            openSections = openSections.filter(function (s) { return s !== name; });
            if (nowOpen) openSections.push(name);
            saveOpen();
          });
        })(group, head, n.section);
        items = document.createElement('div');
        items.className = 'dc-nav-items';
        group.appendChild(head);
        group.appendChild(items);
        nav.appendChild(group);
        return;
      }
      var base = 'display:flex;align-items:center;gap:10px;padding:8px 12px;border-radius:8px;text-decoration:none;' +
        'font-size:14px;white-space:nowrap;';
      var isActive = active.item === n;
      var el = document.createElement('a');
      el.href = navHref(n.area, n.file);
      if (isActive) el.setAttribute('aria-current', 'page');
      el.style.cssText = base + (isActive ? 'background:#E6F0EE;color:#0E4742;font-weight:600;' : 'color:#5C574F;');
      el.innerHTML = iconSvg(n.icon) + '<span class="dc-nav-label">' + escapeHtml(n.label) + '</span>';
      el.title = n.label;
      items.appendChild(el);
    });
    aside.appendChild(nav);

    // Wide screens: the button collapses the rail to icons (remembered across pages). Narrow
    // screens: the rail is a top bar and the same button opens/closes the menu (not remembered).
    var STORAGE_KEY = 'purrcare-nav-collapsed';
    var narrow = window.matchMedia('(max-width: 900px)');
    var icon = btn.querySelector('.dc-nav-toggle-icon');
    function syncButton() {
      var expanded = narrow.matches
        ? aside.classList.contains('dc-aside-mobile-open')
        : !aside.classList.contains('dc-aside-collapsed');
      btn.setAttribute('aria-expanded', String(expanded));
      btn.setAttribute('aria-label', narrow.matches ? (expanded ? 'Close menu' : 'Open menu')
        : (expanded ? 'Collapse menu' : 'Expand menu'));
      icon.style.transform = aside.classList.contains('dc-aside-collapsed') ? 'rotate(180deg)' : 'none';
    }
    var collapsed = false;
    try { collapsed = window.localStorage.getItem(STORAGE_KEY) === '1'; } catch (e) { /* ignore */ }
    aside.classList.toggle('dc-aside-collapsed', collapsed);
    syncButton();
    btn.addEventListener('click', function () {
      if (narrow.matches) {
        aside.classList.toggle('dc-aside-mobile-open');
      } else {
        var next = !aside.classList.contains('dc-aside-collapsed');
        aside.classList.toggle('dc-aside-collapsed', next);
        try { window.localStorage.setItem(STORAGE_KEY, next ? '1' : '0'); } catch (e) { /* ignore */ }
      }
      syncButton();
    });
    if (narrow.addEventListener) narrow.addEventListener('change', syncButton);
    return aside;
  }

  function buildBreadcrumb(active) {
    var nav = document.createElement('nav');
    nav.setAttribute('aria-label', 'Breadcrumb');
    nav.style.cssText = 'display:flex;align-items:center;gap:8px;font-size:14px;color:#5C574F;min-width:0;';
    if (active.item) {
      nav.innerHTML = '<span>' + escapeHtml(active.section) + '</span><span aria-hidden="true">/</span>' +
        '<a href="' + navHref(active.item.area, active.item.file) + '" style="color:#1C1A17;font-weight:600;text-decoration:none;">' +
        escapeHtml(active.item.label) + '</a>';
    } else {
      nav.innerHTML = '<span style="color:#1C1A17;font-weight:600;">' + escapeHtml(document.title.split(' — ')[0]) + '</span>';
    }
    return nav;
  }

  function buildIdentity(who) {
    var box = document.createElement('div');
    box.className = 'dc-identity';
    box.style.cssText = 'display:flex;align-items:center;gap:14px;flex-shrink:0;';
    if (who.branchSelect) {
      who.branchSelect.style.cssText = 'font:inherit;font-size:13px;padding:5px 8px;border:1px solid #CBC3B2;border-radius:6px;background:#fff;';
      box.appendChild(who.branchSelect);
    }
    var line = document.createElement('span');
    line.style.cssText = 'font-size:13px;color:#5C574F;white-space:nowrap;';
    line.textContent = [who.name, who.role].filter(Boolean).join(' · ');
    if (who.note) line.title = who.note;
    box.appendChild(line);
    return box;
  }

  // Every staff header: 56px, breadcrumb/title on the left, everything else grouped on the right
  // with the short identity always last — the top-right corner.
  function normalizeHeader(header, who) {
    header.style.cssText = 'height:56px;min-height:56px;flex-shrink:0;display:flex;align-items:center;' +
      'justify-content:space-between;gap:16px;padding:0 26px;border-bottom:1px solid #E5E1D8;background:#FFFFFF;' +
      'box-sizing:border-box;';

    Array.prototype.slice.call(header.children).forEach(function (child, i) {
      if (i > 0 && who.name && child.textContent.indexOf(who.name) !== -1) header.removeChild(child);
    });

    var left = header.firstElementChild;
    if (!left) {
      left = document.createElement('span');
      header.insertBefore(left, header.firstChild);
    }
    var right = document.createElement('div');
    right.style.cssText = 'display:flex;align-items:center;gap:16px;margin-left:auto;';
    Array.prototype.slice.call(header.children).slice(1).forEach(function (c) { right.appendChild(c); });

    var identity = buildIdentity(who);
    if (right.children.length) identity.style.cssText += 'border-left:1px solid #E5E1D8;padding-left:16px;';
    right.appendChild(identity);
    header.appendChild(right);
  }

  function makeRow() {
    var row = document.createElement('div');
    row.className = 'dc-shell-row';
    row.style.cssText = 'display:flex;width:100%;min-height:100vh;background:#FAF9F6;';
    return row;
  }

  function textWithoutSelects(el) {
    var out = '';
    Array.prototype.slice.call(el.childNodes).forEach(function (n) {
      if (n.nodeType === Node.TEXT_NODE) out += n.nodeValue;
      else if (n.nodeType === Node.ELEMENT_NODE && n.tagName !== 'SELECT' && n.tagName !== 'A' &&
               !n.classList.contains('avatar')) out += textWithoutSelects(n);
    });
    return out;
  }

  // Administration, Accounts & Identity and Notifications staff screens: a .topnav ("PurrCare ·
  // staff" + identity), sometimes a flat .sidebar, then the page content.
  function shellFromTopnav(root) {
    var topnav = root.querySelector('.topnav');
    if (!topnav) return false;
    var brandEl = topnav.querySelector('.brand');
    if (!brandEl || brandEl.textContent.indexOf('staff') === -1) return false;

    var whoEl = topnav.querySelector('.who') || topnav.lastElementChild;
    var parts = whoEl && whoEl !== brandEl
      ? textWithoutSelects(whoEl).trim().split('·').map(function (s) { return s.trim(); })
      : [];
    var who = {
      name: parts[0] || '',
      role: parts[1] || '',
      branchSelect: whoEl ? whoEl.querySelector('select') : null
    };

    var page = topnav.parentElement;
    var sidebar = page.querySelector('.sidebar');
    if (sidebar) sidebar.parentNode.removeChild(sidebar);
    topnav.parentNode.removeChild(topnav);

    var active = activeNav();
    var column = document.createElement('div');
    column.className = 'dc-content-col';
    column.style.cssText = 'flex:1 1 auto;min-width:0;display:flex;flex-direction:column;';
    while (page.firstChild) column.appendChild(page.firstChild);

    var header = document.createElement('header');
    header.appendChild(buildBreadcrumb(active));
    column.insertBefore(header, column.firstChild);
    normalizeHeader(header, who);

    // .main was a flex item beside the old sidebar; flex items won't shrink below their widest
    // child by default, which pushed tables and buttons off-screen on a phone.
    Array.prototype.slice.call(column.querySelectorAll('.main, .shell')).forEach(function (m) {
      m.style.minWidth = '0';
    });

    var row = makeRow();
    row.appendChild(buildAside(active));
    row.appendChild(column);
    page.appendChild(row);
    return true;
  }

  // Cat Profile list screens (Main, Vaccines): an <aside> (each with its own nav and a bottom
  // identity block) beside a content column that already has a header.
  function shellFromCatAside(root) {
    var aside = root.querySelector('aside');
    if (!aside) return false;
    var row = aside.parentElement;
    var column = aside.nextElementSibling;
    if (!column) return false;

    var active = activeNav();
    row.replaceChild(buildAside(active), aside);
    row.classList.add('dc-shell-row');

    var header = column.querySelector(':scope > header');
    if (!header) {
      header = document.createElement('header');
      column.insertBefore(header, column.firstChild);
    }
    // These screens put the page title (and its primary action) in the header bar itself. Every
    // other staff screen has the breadcrumb there and the title just below — move it down to match.
    if (header.firstElementChild && header.firstElementChild.tagName === 'H1') {
      var titleRow = document.createElement('div');
      titleRow.style.cssText = 'display:flex;align-items:center;justify-content:space-between;gap:16px;padding:22px 26px 0;';
      while (header.firstElementChild) titleRow.appendChild(header.firstElementChild);
      column.insertBefore(titleRow, header.nextSibling);
    }
    if (!header.querySelector('nav[aria-label="Breadcrumb"]')) {
      header.insertBefore(buildBreadcrumb(active), header.firstChild);
    }
    normalizeHeader(header, CAT_PROFILE_WHO);
    column.style.minWidth = '0';
    return true;
  }

  // Cat Profile detail screens: no sidebar at all, just a breadcrumb header (sometimes with the
  // identity top-right) over the content.
  function shellFromCatBreadcrumb(root) {
    var crumb = root.querySelector('header > nav[aria-label="Breadcrumb"]');
    if (!crumb) return false;
    var header = crumb.parentElement;
    var column = header.parentElement;
    if (!column || column.parentNode !== root) return false;

    normalizeHeader(header, CAT_PROFILE_WHO);

    var row = makeRow();
    root.insertBefore(row, column);
    row.appendChild(buildAside(activeNav()));
    row.appendChild(column);
    column.style.width = 'auto';
    column.style.maxWidth = 'none';
    column.style.height = 'auto';
    column.style.flex = '1 1 auto';
    column.style.minWidth = '0';
    return true;
  }

  // The sidebar is position:sticky at full viewport height. Any scrolling/clipping ancestor
  // (Administration's artboard is overflow:auto) would become its scroll container and pin it
  // there instead of to the page, so it would scroll away and stop short.
  function releaseStickyAncestors(root) {
    var aside = root.querySelector('.dc-aside');
    var p = aside ? aside.parentElement : null;
    while (p && p !== root) {
      var ov = getComputedStyle(p).overflow;
      if (ov !== 'visible' && ov !== '') p.style.overflow = 'visible';
      p = p.parentElement;
    }
  }

  function applyStaffShell(root) {
    // The aside/breadcrumb shapes are Cat Profile's own staff layouts; elsewhere an <aside> or a
    // breadcrumb header is ordinary page content (a storefront filter panel, a portal crumb).
    var catProfile = AREA === 'cat-profile';
    if (shellFromTopnav(root) || (catProfile && (shellFromCatAside(root) || shellFromCatBreadcrumb(root)))) {
      releaseStickyAncestors(root);
    }
  }

  /* ---------------------------------------------------------------- fluid layout */

  function injectGlobalStyles() {
    var style = document.createElement('style');
    style.setAttribute('data-dc-support', '');
    style.textContent =
      'html,body{margin:0;padding:0}' +
      'img,svg{max-width:100%}' +
      '.dc-scroll-x{overflow-x:auto;max-width:100%}' +
      '.dc-aside{transition:width .15s ease,padding .15s ease}' +
      '.dc-nav-toggle-icon{transition:transform .15s ease}' +
      '.dc-nav-menu-icon{display:none}' +
      '.dc-aside nav a:not([aria-current]):hover{background:#EAE6DC}' +
      '.dc-nav-section:hover{color:#5C574F !important}' +
      '.dc-nav-chevron{transition:transform .15s ease}' +
      '.dc-nav-items{display:flex;flex-direction:column;gap:2px}' +
      '.dc-nav-group.dc-closed .dc-nav-items{display:none}' +
      '.dc-nav-group.dc-closed .dc-nav-chevron{transform:rotate(-90deg)}' +
      '@media (min-width:901px){' +
      '.dc-aside-collapsed{width:68px !important;padding-left:12px !important;padding-right:12px !important}' +
      '.dc-aside-collapsed .dc-nav-label{display:none !important}' +
      '.dc-aside-collapsed .dc-nav-toggle-row{flex-direction:column;gap:12px}' +
      '.dc-aside-collapsed .dc-nav-items > a{justify-content:center}' +
      '.dc-aside-collapsed .dc-nav-group.dc-closed .dc-nav-items{display:flex}' +
      '.dc-aside-collapsed .dc-nav-group + .dc-nav-group{border-top:1px solid #DDD8CD;margin-top:6px;padding-top:6px}' +
      '}' +
      '@media (max-width:900px){' +
      '.dc-shell-row{flex-direction:column !important}' +
      '.dc-shell-row > .dc-aside{position:sticky !important;top:0;z-index:30;height:auto !important;width:100% !important;' +
      'padding:10px 16px !important;gap:10px !important;border-right:none !important;border-bottom:1px solid #E5E1D8}' +
      '.dc-shell-row > .dc-aside nav{display:none !important}' +
      '.dc-shell-row > .dc-aside.dc-aside-mobile-open nav{display:flex !important;position:absolute;left:0;right:0;top:100%;' +
      'max-height:calc(100vh - 60px);overflow-y:auto;background:#F3F1EB;padding:8px 16px 14px;' +
      'border-bottom:1px solid #E5E1D8;box-shadow:0 12px 24px rgba(28,26,23,.12)}' +
      '.dc-nav-toggle-icon{display:none}' +
      '.dc-nav-menu-icon{display:block}' +
      '.dc-shell-row header{height:auto !important;min-height:56px !important;flex-wrap:wrap;row-gap:6px;' +
      'padding:10px 16px !important}' +
      '.dc-shell-row header > *{min-width:0}' +
      '.dc-identity{min-width:0;flex-shrink:1}' +
      '.dc-identity > span{overflow:hidden;text-overflow:ellipsis}' +
      '}';
    document.head.appendChild(style);
  }

  function sumGridTracks(template, gapPx) {
    var tracks = template.trim().split(/\s+/);
    var sum = 0;
    tracks.forEach(function (t) {
      var m = /^([\d.]+)px$/.exec(t);
      if (m) sum += parseFloat(m[1]);
    });
    return sum + gapPx * Math.max(0, tracks.length - 1);
  }

  function nearestClippingAncestor(el) {
    var p = el.parentElement;
    while (p && !(p.style && p.style.overflow === 'hidden')) p = p.parentElement;
    return p;
  }

  function fluidize(root) {
    var artboard = root.firstElementChild;
    if (artboard) {
      artboard.style.width = '100%';
      artboard.style.maxWidth = 'none';
      artboard.style.height = 'auto';
      artboard.style.minHeight = '100vh';
    }

    Array.prototype.slice.call(root.querySelectorAll('*')).forEach(function (el) {
      var s = el.style;
      if (el.classList.contains('dc-aside')) return;

      if (s.width && /px$/.test(s.width)) {
        s.maxWidth = '100%';
      }

      if (s.display === 'grid' && s.gridTemplateColumns) {
        var gapPx = parseFloat(s.gap || s.columnGap || '0') || 0;
        var minW = sumGridTracks(s.gridTemplateColumns, gapPx);
        if (minW > 0) s.minWidth = minW + 'px';
        var clipAncestor = nearestClippingAncestor(el);
        if (clipAncestor) {
          clipAncestor.style.overflow = '';
          clipAncestor.style.overflowX = 'auto';
          clipAncestor.style.overflowY = 'visible';
        }
      }

      if (el.tagName === 'TABLE' && !(el.parentElement && el.parentElement.classList.contains('dc-scroll-x'))) {
        var wrap = document.createElement('div');
        wrap.className = 'dc-scroll-x';
        el.parentNode.insertBefore(wrap, el);
        wrap.appendChild(el);
      }
    });
  }

  /* ---------------------------------------------------------------- boot */

  function boot() {
    injectGlobalStyles();

    var root = document.querySelector('x-dc');
    if (!root) return;

    var helmet = root.querySelector(':scope > helmet');
    if (helmet) {
      Array.prototype.slice.call(helmet.children).forEach(function (el) { document.head.appendChild(el); });
      helmet.remove();
    }

    var templateFrag = document.createDocumentFragment();
    Array.prototype.slice.call(root.childNodes).forEach(function (n) { templateFrag.appendChild(n); });

    var scriptEl = document.querySelector('script[type="text/x-dc"][data-dc-script]');
    var props = {};
    if (scriptEl) {
      try { props = JSON.parse(scriptEl.getAttribute('data-props') || '{}'); } catch (e) { /* ignore */ }
    }

    var ComponentClass = null;
    if (scriptEl) {
      try {
        var factory = new Function('DCLogic', scriptEl.textContent + '\nreturn Component;');
        ComponentClass = factory(DCLogic);
      } catch (e) {
        console.error('dc-support: failed to evaluate component script', e);
      }
    }

    var instance = ComponentClass ? new ComponentClass(props) : new DCLogic(props);

    function doRender() {
      var vals = {};
      try { vals = instance.renderVals() || {}; } catch (e) { console.error('dc-support: renderVals() threw', e); }
      var clone = templateFrag.cloneNode(true);
      processChildren(clone, vals);
      while (root.firstChild) root.removeChild(root.firstChild);
      root.appendChild(clone);
      applyStaffShell(root);
      fluidize(root);
    }

    instance._render = doRender;
    doRender();
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', boot);
  } else {
    boot();
  }
})();
