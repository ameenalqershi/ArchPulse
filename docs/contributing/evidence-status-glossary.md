# Evidence status glossary — دليل حالات الأدلة

> S00 / S26 / T001 contributor guide. These labels describe **what evidence supports a claim**, not how complete or safe a feature is. The canonical evidence enum is in [`EvidenceReceipt.cs`](../../engine/ArchPulse.Core/EvidenceReceipt.cs). See the [specification index](../requirements/specification-index.md) and [agent rules](../../AGENTS.md).

## English

| Status | What it means | Public, non-sensitive example | It does **not** mean |
| --- | --- | --- | --- |
| **Designed / Proposed** (`Proposed`) | A planned relationship, rule or design option. It is not a code or runtime observation. | A proposed Mermaid arrow says “API → Web Studio” in an RFC. | The API integration exists or works. |
| **Static** | A claim derived from source code or build metadata at a specified revision. | Roslyn finds `InvoiceService.Calculate` calling `RateCalculator.Apply` at commit `abc123` in a toy fixture. | That call occurred in a running process. |
| **Observed** | A bounded execution, measurement or test result tied to an environment and revision. | On a named Linux test runner, a single test run records that a local sample call returned normally at revision `abc123`. | Every environment or input behaves the same way. |
| **Inferred** | A hypothesis supported by some indications but lacking decisive validation. | A controller named `OrdersController` may implement the ordering feature. | The business owner confirmed that feature classification. |
| **Verified** | A **specific claim** independently reproduced with recorded source/revision and relevant evidence. | A reviewer reruns a pinned test case against the cited commit and corroborates its expected result. | The entire product, unrelated requirements or all security properties are accepted. |
| **Unknown** | Evidence is absent, disconnected, interrupted or insufficient; this is explicitly undecided. | A trace collector was offline, so the execution status of a method is Unknown. | The method did not execute, passed or failed. |

The C# evidence enum calls the planned state `Proposed`; contributor diagrams may say **Designed** as a synonym for intended architecture. A stored `Verified` receipt is **not self-authenticating**: the current minimal type validates the presence of source and revision but does not independently authenticate either. An approved ADR may authorize a *design*; it does not by itself verify the implementation.

### Two distinctions every contributor must preserve

- **Green PR CI ≠ product capability acceptance.** A successful test on a PR establishes only the bounded conditions actually tested on that exact PR SHA. Full capability acceptance requires the Sxx/Txxx criteria, integration/security review, and independently reproducible evidence.
- **No runtime trace ≠ method not executed.** An absent OpenTelemetry span may indicate sampling, outage, uninstrumented code, lost transport or simply no observed data. Record `Unknown` unless supported evidence establishes the narrow claim.

### Example decision review

A proposed RFC says “the order handler never sends sensitive values to the logger.” Label the design **Proposed**. Source inspection of a specific revision may yield **Static** findings for known calls; an instrumented test may yield **Observed** data for its own environment. If a logger is opaque or a collector is disconnected, preserve **Unknown** for that path. Mark a **narrow** statement **Verified** only after an independent reviewer corroborates the exact revision, bounded tests and relevant evidence. Do not promote the entire logging/privacy policy to Verified from a green PR build. Keep rejected or uncertain hypotheses as **Inferred** or **Unknown**, and document next verification steps.

## العربية

| الحالة | المعنى الهندسي | مثال عام لا يتضمن بيانات حساسة | ما لا تثبته |
| --- | --- | --- | --- |
| **مصمَّم / مقترح — Designed / Proposed** | علاقة أو قاعدة أو خيار معماري مخطط له، دون دليل من الشيفرة أو التشغيل. | سهم Mermaid مقترح من واجهة API إلى Web Studio في وثيقة تصميم. | وجود التكامل أو نجاح تشغيله. |
| **ثابت من المصدر — Static** | حقيقة محدودة مستخرجة من الشيفرة أو بيانات البناء في إصدار محدد. | اكتشاف Roslyn استدعاء دالة حسابية لأخرى في مثال تجريبي عند Commit معلوم. | وقوع الاستدعاء بالفعل أثناء التشغيل. |
| **مرصود — Observed** | مشاهدة تنفيذ أو قياس أو اختبار محدود مع تحديد البيئة والإصدار. | تسجيل نتيجة اختبار واحد لمثال محلي على Linux بإصدار محدد. | صحة جميع المدخلات والبيئات أو اكتمال الميزة. |
| **مستنتَج — Inferred** | فرضية تدعمها مؤشرات وتحتاج إثباتًا مستقلًا. | الاستنتاج أن `OrdersController` يتبع وظيفة الطلبات اعتمادًا على الاسم. | موافقة مالك المجال على تصنيف الوظيفة. |
| **متحقَّق منه — Verified** | ادعاء محدد أعيد التحقق منه باستقلالية مع دليل ومصدر وإصدار قابلين للتتبع. | أعاد مراجع تنفيذ اختبار مثبت على Commit معين وراجع النتيجة. | قبول المنتج بالكامل أو اثبات جميع الخصائص الأمنية. |
| **مجهول — Unknown** | نقص الأدلة أو انقطاعها أو فشل جمعها؛ لا يصح الجزم بالنتيجة. | تعطل جامع التتبعات، لذا حالة تنفيذ الدالة مجهولة. | أن الدالة لم تعمل، أو نجحت، أو فشلت. |

تستخدم الشيفرة اسم `Proposed` للحالة المقترحة، ويمكن كتابة «مصمَّم / Designed» في المخططات للدلالة على **الهدف المخطط** لا التنفيذ. وجود إيصال برمجي يحمل `Verified` لا يصادق تلقائيًا على صحة المصدر؛ فالعقد الحالي يتحقق من وجود المصدر والإصدار فقط ويتطلب إثباتًا مستقلاً.

### فروق ملزمة للمساهمين

- **نجاح CI لطلب الدمج لا يعني قبول الميزة إنتاجيًا.** هو دليل على الاختبارات التي نُفذت في SHA محدد فقط، بينما يتطلب قبول الميزة استيفاء معايير المهمة والمواصفة ومراجعة التكامل والأمان.
- **غياب التتبّع التشغيلي لا يثبت عدم تنفيذ الدالة.** قد يكون السبب أخذ عينات أو انقطاع الاتصال أو عدم تفعيل القياس؛ سجّل `Unknown` بدل الحكم بالنجاح أو الفشل أو عدم التنفيذ.

### مثال مراجعة قرار

تقترح وثيقة تصميم أن معالج الطلبات لا يرسل بيانات حساسة إلى السجل. نسمّي الادعاء **مقترحًا**. قد يقدم تحليل الشيفرة لإصدار معين دليلًا **ثابتًا** عن الاستدعاءات، ويقدم اختبار مُراقَب دليلًا **مرصودًا** ضمن بيئته المحدودة. إذا كانت المكتبة غير قابلة للتحليل أو كان جمع التتبعات منقطعًا، تبقى الحالة **مجهولة**. لا نستخدم **متحقَّق منه** إلا لادعاء ضيق أعاد مراجع مستقل فحصه مع الأدلة والإصدار؛ ولا نحول نجاح CI إلى إثبات لسلامة جميع مسارات التسجيل.

## Contributor checklist — قائمة المساهمة

Attach the exact revision, source location or bounded runner receipt to each claim; state environment and unsupported cases; keep Unknown explicit; request human review of architectural wording before accepting a status change. **لا تُخفِ حالة المجهول، واربط كل ادعاء بالدليل والإصدار، واطلب مراجعة بشرية قبل اعتماد تغيير معماري.**
