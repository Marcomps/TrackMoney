# TrackTrace Money

**[English](README.md) | Español**

> **Track. Trace. Control.**
> Aplicación móvil para el control, seguimiento y análisis de las finanzas personales.

---

## 1. Descripción general

**TrackTrace Money** será una aplicación móvil desarrollada con **.NET MAUI**, orientada al control integral de las finanzas personales.

La aplicación permitirá registrar y analizar:

* Ingresos
* Gastos
* Transferencias
* Cuentas bancarias
* Ahorros
* Tarjetas de crédito
* Préstamos y créditos
* Depósitos a plazo
* Fondos de inversión
* Presupuestos
* Deudas
* Patrimonio
* Gastos médicos
* Gastos de terceros
* Seguros y reembolsos
* Pagos recurrentes
* Recordatorios
* Reportes financieros

El objetivo principal no será solamente registrar gastos, sino **permitir al usuario conocer el recorrido de su dinero**:

> **De dónde viene → dónde está → en qué se utiliza → qué debe → qué está ahorrando → qué está invirtiendo → cuánto realmente posee.**

---

# 2. Principios del sistema

## 2.1 Offline First

La aplicación deberá funcionar principalmente **sin conexión a Internet**.

Todas las operaciones principales deberán funcionar offline:

* Crear movimientos
* Editar movimientos
* Eliminar movimientos
* Consultar historial
* Consultar cuentas
* Registrar gastos
* Registrar ingresos
* Registrar pagos
* Consultar deudas
* Calcular presupuestos
* Generar reportes locales
* Mostrar dashboards
* Crear recordatorios locales

La conexión a Internet **no será requisito para utilizar la aplicación**.

---

## 2.2 Base de datos local

La base de datos principal será **SQLite**.

SQLite será la fuente de verdad del dispositivo y no simplemente una caché.

Se deberá contemplar:

* Migraciones
* Seeds iniciales
* Integridad referencial
* Transacciones
* Índices
* Backup local
* Restauración

---

## 2.3 Nube opcional

La nube será una capacidad posterior y opcional.

Su propósito será:

* Backup
* Restauración
* Sincronización
* Consulta desde múltiples dispositivos
* Posible aplicación web futura

La aplicación deberá seguir funcionando aunque el usuario nunca active servicios en la nube.

---

# 3. Plataforma y arquitectura

## 3.1 Tecnología

### Frontend

* .NET MAUI
* C#
* XAML
* MVVM
* CommunityToolkit.Mvvm
* CommunityToolkit.Maui

### Persistencia

* SQLite
* Entity Framework Core
* EF Core SQLite

### Futuro backend

* ASP.NET Core Web API
* PostgreSQL
* Docker
* Autenticación
* Servicio de sincronización

---

# 4. Arquitectura propuesta

```text
TrackTrace Money
│
├── TrackTraceMoney.App
│   ├── Views
│   ├── ViewModels
│   ├── Resources
│   ├── Navigation
│   └── Styles
│
├── TrackTraceMoney.Domain
│   ├── Entities
│   ├── Enums
│   └── Business Rules
│
├── TrackTraceMoney.Application
│   ├── Services
│   ├── Interfaces
│   ├── DTOs
│   └── Use Cases
│
├── TrackTraceMoney.Infrastructure
│   ├── SQLite
│   ├── EF Core
│   ├── Repositories
│   ├── Migrations
│   └── Local Notifications
│
└── TrackTraceMoney.Api       # Futuro
    ├── Controllers
    ├── Authentication
    ├── Synchronization
    └── PostgreSQL
```

---

# 5. Idiomas

La aplicación deberá soportar inicialmente:

* 🇪🇸 Español
* 🇺🇸 Inglés

La localización deberá implementarse desde el comienzo.

No se deberán colocar textos directamente en las vistas cuando sean textos de interfaz.

Se utilizarán recursos localizados, por ejemplo:

```text
Resources/
├── AppResources.resx
└── AppResources.en.resx
```

El usuario podrá cambiar el idioma desde configuración.

---

# 6. Moneda

El sistema deberá permitir seleccionar la moneda principal.

Ejemplos:

* USD
* EUR
* MXN
* CRC
* GTQ
* HNL
* NIO
* PAB

La moneda deberá almacenarse explícitamente en las cuentas y operaciones cuando corresponda.

---

# 7. Personas

El sistema deberá permitir registrar personas relacionadas con gastos.

Ejemplo:

```text
Persona
├── Nombre
├── Relación
└── Notas
```

Relaciones predefinidas:

* Yo
* Pareja
* Hijo/a
* Padre/madre
* Familiar
* Otro

También se podrán crear relaciones personalizadas.

Esto permitirá diferenciar:

> **Quién pagó** de **para quién fue el gasto**.

---

# 8. Cuentas financieras

La aplicación deberá distinguir los diferentes tipos de productos financieros.

## 8.1 Cuentas de activos

* Efectivo
* Cuenta bancaria
* Cuenta corriente
* Cuenta de ahorro
* Depósito a plazo
* Fondo de inversión

## 8.2 Cuentas de pasivos

* Tarjeta de crédito
* Préstamo
* Crédito bancario

Conceptualmente:

```text
FinancialAccount
│
├── Cash
├── BankAccount
├── SavingsAccount
├── TermDeposit
└── InvestmentFund

CreditAccount
│
├── CreditCard
└── Loan
```

---

# 9. Movimientos

El sistema deberá permitir registrar diferentes tipos de movimientos.

```text
Income
Expense
Transfer
CreditCardPurchase
CreditCardPayment
LoanPayment
InvestmentContribution
InvestmentWithdrawal
InterestIncome
Reimbursement
```

## 9.1 Regla fundamental

No todos los movimientos representan un gasto.

Ejemplo:

```text
Banco A → Tarjeta de crédito
$300
```

Esto representa un **pago de deuda/transferencia**, no un nuevo gasto.

La compra original sí fue un gasto.

Esto evitará duplicar gastos.

---

# 10. Ingresos

El usuario podrá registrar:

* Salario
* Bonificaciones
* Comisiones
* Freelance
* Intereses
* Dividendos
* Reembolsos
* Otros ingresos

Campos:

```text
Monto
Descripción
Categoría
Fecha
Cuenta destino
Persona
Notas
```

---

# 11. Gastos

El usuario podrá registrar:

```text
Monto
Descripción
Categoría
Fecha
Método de pago
Cuenta
Persona beneficiaria
Notas
```

Métodos de pago:

* Efectivo
* Cuenta bancaria
* Cuenta de ahorro
* Tarjeta de crédito

Ejemplo:

```text
$75.50
Supermercado
04/09/2026
Tarjeta BAC
```

---

# 12. Categorías

El usuario podrá administrar categorías.

Categorías iniciales:

* Alimentación
* Vivienda
* Transporte
* Salud
* Educación
* Entretenimiento
* Compras
* Servicios
* Suscripciones
* Deudas
* Seguros
* Inversiones
* Ahorro
* Otros

Las categorías deberán poder personalizarse.

---

# 13. Transferencias

Se podrán transferir fondos entre cuentas.

Ejemplo:

```text
Banco A
   ↓
$500
   ↓
Cuenta de ahorro
```

Una transferencia:

* Reduce el saldo de la cuenta origen.
* Incrementa el saldo de la cuenta destino.
* No se contabiliza como gasto.

---

# 14. Tarjetas de crédito

Cada tarjeta deberá almacenar:

```text
Banco / emisor
Nombre
Últimos 4 dígitos
Límite de crédito
Saldo utilizado
Saldo disponible
Tasa anual
Tasa mensual
Fecha de corte
Fecha límite de pago
Pago mínimo
Pago para evitar intereses
Último estado de cuenta
Estado
```

Ejemplo:

```text
💳 Tarjeta BAC

Límite:              $2,000
Utilizado:             $650
Disponible:          $1,350

Corte:                    25
Pago límite:              10

Pago mínimo:             $50
Pago para no intereses: $500
```

---

# 15. Ciclos y estados de cuenta

Las tarjetas deberán manejar ciclos de facturación.

Ejemplo:

```text
Fecha de corte: 25

26 agosto
     ↓
25 septiembre
     ↓
Estado de cuenta
     ↓
10 octubre
Fecha límite de pago
```

Se deberá poder identificar qué compras pertenecen a cada estado de cuenta.

---

# 16. Pago de tarjeta

Deberá existir una operación específica:

```text
Pago de tarjeta

Tarjeta:
BAC

Cuenta utilizada:
Banco principal

Monto:
$300

Fecha:
10/09/2026
```

Resultado:

```text
Banco
-$300

Tarjeta
-$300 de deuda
```

No deberá aumentar los gastos del período.

---

# 17. Análisis "Comprado vs Pagado"

La aplicación deberá comparar:

```text
Compras de tarjeta
vs
Pagos realizados
```

Ejemplo:

```text
Compras:     $650
Pagos:       $400
----------------
Diferencia: +$250
```

Indicador:

🟠 **Estás comprando más de lo que estás pagando.**

Si:

```text
Compras:     $650
Pagos:       $800
```

Mostrar:

🟢 **Estás pagando $150 más de lo que estás comprando.**

El análisis podrá realizarse por:

* Mes
* Ciclo
* 3 meses
* 6 meses
* Año

---

# 18. Semáforo de tarjetas

Las tarjetas deberán mostrar un indicador visual.

### 🟢 Verde

Situación saludable.

Ejemplo:

* Pago para evitar intereses cubierto.
* No existen pagos vencidos.

### 🟡 Amarillo

Atención.

Ejemplo:

* Se aproxima la fecha límite.
* El pago para evitar intereses aún no está completo.

### 🟠 Naranja

Situación de riesgo.

Ejemplo:

* Pago parcial.
* Alto porcentaje del límite utilizado.

### 🔴 Rojo

Situación crítica.

Ejemplo:

* Pago vencido.
* Obligación pendiente después de la fecha límite.

La aplicación deberá diferenciar:

```text
Pago mínimo
```

de:

```text
Pago para evitar intereses
```

No deberá asumir automáticamente las reglas específicas de intereses de cada banco.

El valor de **"pago para evitar intereses"** será introducido por el usuario a partir del estado de cuenta.

---

# 19. Préstamos y créditos

Se podrán registrar:

* Crédito personal
* Crédito vehicular
* Hipoteca
* Crédito bancario
* Compra a cuotas
* Otro préstamo

Campos:

```text
Institución
Nombre
Monto original
Saldo actual
Tasa de interés
Tipo de tasa
Cuota mensual
Fecha de próximo pago
Pagos restantes
Pago requerido
Comisiones
Notas
```

Ejemplo:

```text
🏦 Crédito personal

Original:        $5,000
Saldo:           $3,250
Tasa:               12%
Cuota:             $150
Próximo pago: 15/09/2026
Restantes:          24
```

---

# 20. Bola de Nieve

La aplicación deberá incluir una estrategia de pago de deudas basada en **Bola de Nieve**.

Primero se deberán cubrir los pagos mínimos/obligatorios.

Después, el dinero adicional se dirigirá a la deuda con menor saldo.

Ejemplo:

```text
Tarjeta A       $250
Tarjeta B       $800
Tarjeta C     $2,500
Préstamo      $5,000
```

Orden:

```text
1. $250
2. $800
3. $2,500
4. $5,000
```

Cuando una deuda sea eliminada:

```text
Pago liberado
      ↓
Siguiente deuda
```

El sistema deberá mostrar:

```text
⭐ OBJETIVO ACTUAL

Tarjeta A

Saldo:       $250
Mínimo:       $25
Extra sugerido: $150
```

La Bola de Nieve será una estrategia configurable y no deberá imponerse al usuario.

Como futura mejora podrá incorporarse también:

* Avalancha
* Pago personalizado

---

# 21. Ahorros

Se podrán registrar cuentas destinadas al ahorro.

Ejemplo:

```text
Cuenta:
Fondo de emergencia

Saldo:
$2,500

Objetivo:
$5,000
```

Se podrá mostrar:

```text
$2,500 / $5,000
██████████░░░░░░
50%
```

---

# 22. Depósitos a plazo

Se deberán poder registrar depósitos a plazo.

Campos:

```text
Institución
Nombre
Capital inicial
Tasa
Tipo de tasa
Fecha de inicio
Fecha de vencimiento
Periodicidad de intereses
Capitalización
Interés estimado
Interés recibido
Renovación automática
Moneda
Notas
```

La aplicación podrá generar recordatorios antes del vencimiento.

---

# 23. Fondos de inversión

Se podrán registrar inversiones.

Campos:

```text
Fondo
Institución
Fecha de inversión
Aportes
Retiros
Valor actual
Rendimiento
Comisiones
Moneda
Notas
```

Ejemplo:

```text
Inversión inicial: $2,000
Valor actual:      $2,084.50

Ganancia:             $84.50
Rendimiento:           4.23%
```

Se podrá almacenar un historial del valor de la inversión para generar gráficos.

---

# 24. Patrimonio neto

El sistema deberá calcular el patrimonio neto.

### Activos

```text
Efectivo
+ Bancos
+ Ahorros
+ Depósitos a plazo
+ Inversiones
```

### Pasivos

```text
Tarjetas
+ Préstamos
+ Créditos
```

### Fórmula

```text
Patrimonio Neto =
Activos - Pasivos
```

Ejemplo:

```text
Activos:       $8,784
Deudas:        $3,850
---------------------
Patrimonio:    $4,934
```

Se deberá poder visualizar la evolución del patrimonio a través del tiempo.

---

# 25. Gastos médicos

La aplicación deberá permitir registrar gastos relacionados con:

* Médico
* Dentista
* Medicamentos
* Hospital
* Laboratorio
* Terapias
* Otros servicios médicos

El gasto deberá permitir indicar:

```text
Proveedor
Paciente / beneficiario
Monto
Fecha
Cuenta utilizada
Método de pago
Seguro utilizado
Monto cubierto
Monto reembolsable
Monto esperado
Estado
Notas
```

---

# 26. Gastos médicos de terceros

Se deberá poder indicar para quién se realizó el gasto.

Ejemplo:

```text
Paciente:
Hijo

Proveedor:
Dentista

Costo:
$150

Pagado por:
Usuario

Método:
Tarjeta de crédito
```

Esto permite separar:

```text
Quién pagó
```

de:

```text
Para quién fue el gasto
```

---

# 27. Seguros y reembolsos

Los gastos podrán indicar si fueron cubiertos parcialmente por un seguro.

Ejemplo:

```text
Costo médico:          $150
Seguro:                $100
Costo final:            $50
```

Estados:

🟡 Reembolso pendiente
🟢 Reembolsado
🔴 Rechazado

---

# 28. Reembolsos

Un reembolso recibido deberá registrarse como un **ingreso/recovery relacionado con el gasto**, no como un gasto negativo.

Ejemplo:

```text
Gasto médico:
$150

Reembolso recibido:
$100

Costo real:
$50
```

Un reembolso esperado no deberá considerarse dinero disponible hasta que realmente sea recibido.

---

# 29. Seguro pagando directamente

También deberá contemplarse:

```text
Servicio médico:       $200
Seguro paga:           $150
Usuario paga:           $50
```

En este caso, el impacto real para el usuario será:

```text
$50
```

---

# 30. Dashboard principal

La pantalla principal deberá ser **simple y visual**.

No deberá estar saturada de números.

Debe responder rápidamente:

1. ¿Cuánto tengo?
2. ¿Cuánto estoy gastando?
3. ¿Dónde estoy gastando demasiado?
4. ¿Qué debo pagar?
5. ¿Tengo excedente?
6. ¿Mi deuda está aumentando o disminuyendo?
7. ¿Mi patrimonio está creciendo?

---

# 31. Salud financiera

El dashboard deberá mostrar un indicador general:

```text
MI SALUD FINANCIERA 🟡
```

Estados:

🟢 Saludable
🟡 Atención
🟠 Riesgo
🔴 Crítico
🔵 Oportunidad

---

# 32. Ingresos y gastos

Ejemplo:

```text
INGRESOS
$1,800

GASTOS
$1,050

DISPONIBLE
$750
```

---

# 33. Detección de sobrepresupuesto

El sistema deberá comparar el gasto con el presupuesto.

Ejemplo:

```text
Comida

Gastado:      $420
Presupuesto:  $300

🔴 +$120
```

También podrá mostrar el porcentaje respecto al ingreso:

```text
$420
23.3% de tus ingresos
```

---

# 34. Presupuestos

El usuario podrá establecer presupuestos por:

* Categoría
* Mes
* Periodicidad

Ejemplo:

```text
Comida       $300
Transporte   $200
Entreten.    $100
```

Estados:

🟢 Dentro del presupuesto
🟡 Cerca del límite
🔴 Sobrepasado

---

# 35. Sobrante real

La aplicación deberá diferenciar:

```text
Saldo disponible
```

de:

```text
Sobrante real
```

Ejemplo:

```text
Saldo actual:          $1,000

Gastos próximos:       -$200
Pagos de deuda:        -$250
--------------------------------
Sobrante real:           $550
```

El sobrante real será el dinero que queda después de considerar las obligaciones conocidas.

---

# 36. Recomendaciones sobre el sobrante

Cuando exista sobrante, la aplicación podrá sugerir:

```text
¿Qué hacer con tu excedente?

⛄ Bola de nieve
🛟 Fondo de emergencia
💰 Ahorro
📈 Inversión
⚖️ Distribuir
```

Si el usuario utiliza Bola de Nieve, podrá mostrar:

```text
⭐ Puedes destinar $300 adicionales
a tu deuda objetivo.
```

---

# 37. Recordatorios

La aplicación deberá utilizar notificaciones locales.

Ejemplos:

```text
🔔 Tarjeta BAC

Faltan 7 días para el pago.
```

```text
🔔 Tarjeta BAC

Faltan 3 días.
Pago para evitar intereses:
$500
```

```text
🔴 Tarjeta BAC

El pago vence hoy.
```

También:

* Pagos de préstamos
* Gastos recurrentes
* Reembolsos pendientes
* Vencimiento de depósitos a plazo
* Otros compromisos

Todo esto deberá funcionar offline mediante notificaciones locales.

---

# 38. Gastos recurrentes

El sistema deberá permitir configurar movimientos recurrentes:

* Alquiler
* Internet
* Netflix
* Seguros
* Colegiatura
* Suscripciones
* Servicios
* Otros

Configuración:

```text
Monto
Categoría
Cuenta
Periodicidad
Fecha inicial
Fecha final
```

---

# 39. Historial

Se deberá poder consultar todos los movimientos.

Filtros:

* Fecha
* Categoría
* Cuenta
* Tarjeta
* Persona
* Tipo de movimiento
* Rango de monto
* Estado

Ejemplo:

```text
04 Sep

🛒 Supermercado
-$75.50
Tarjeta BAC

03 Sep

💰 Salario
+$1,800
Banco principal
```

---

# 40. Reportes

Se deberán incluir reportes visuales y fáciles de interpretar.

### Reportes principales

* Gastos por categoría
* Gastos diarios
* Gastos semanales
* Gastos mensuales
* Ingresos vs gastos
* Presupuesto vs gasto
* Compras vs pagos de tarjetas
* Evolución de deudas
* Bola de nieve
* Patrimonio neto
* Gastos médicos
* Gastos por persona
* Gastos cubiertos por seguros
* Reembolsos pendientes
* Reembolsos recibidos
* Rendimiento de inversiones
* Evolución del sobrante

---

# 41. Exportación

Se deberá poder exportar información.

Formatos previstos:

* CSV
* Excel
* PDF

La exportación podrá realizarse por:

* Período
* Cuenta
* Categoría
* Tipo de movimiento
* Reporte

---

# 42. Backup local

La aplicación deberá permitir realizar backups locales.

El backup deberá incluir:

* Configuración
* Categorías
* Personas
* Cuentas
* Movimientos
* Tarjetas
* Estados de cuenta
* Préstamos
* Inversiones
* Presupuestos
* Recordatorios

Idealmente se deberá contemplar cifrado del backup.

---

# 43. Seguridad

Opcionalmente se podrá proteger la aplicación mediante:

* PIN
* Biometría
* Bloqueo automático

Los datos locales deberán almacenarse de forma segura cuando sea técnicamente posible.

---

# 44. Navegación

La navegación principal propuesta:

```text
🏠 Inicio

💸 Movimientos

🏦 Mis cuentas
   ├── Bancos
   ├── Ahorros
   ├── Depósitos a plazo
   └── Fondos de inversión

💳 Créditos
   ├── Tarjetas
   └── Préstamos

📊 Reportes

🎯 Presupuestos

🔔 Recordatorios

⚙️ Configuración
   ├── Idioma
   ├── Moneda
   ├── Seguridad
   └── Backup
```

---

# 45. Botón de acción rápida

La aplicación deberá tener un botón `+`.

Opciones:

```text
+
├── + Ingreso
├── - Gasto
├── ⇄ Transferencia
├── 💳 Pago de tarjeta
├── 🏦 Pago de préstamo
├── 📈 Aporte a inversión
├── 💰 Retiro de inversión
└── 🔄 Reembolso
```

---

# 46. Reglas contables principales

El sistema deberá respetar estas reglas:

### Compra con tarjeta

```text
Gasto + incremento de deuda
```

### Pago de tarjeta

```text
Reducción de banco
+
Reducción de deuda
```

No es un gasto nuevo.

### Transferencia

```text
Cuenta A -
Cuenta B +
```

No es gasto.

### Inversión

```text
Dinero disponible -
Activo de inversión +
```

No debe considerarse automáticamente como gasto.

### Reembolso

```text
Ingreso/recovery +
```

No debe eliminar físicamente el gasto original.

---

# 47. Modelo conceptual de datos

```text
User
│
├── Person
├── Category
├── FinancialAccount
│
├── Transaction
│
├── CreditCard
│   ├── CreditCardStatement
│   └── CreditCardPayment
│
├── Loan
│
├── Budget
│
├── RecurringTransaction
│
├── MedicalExpense
│   ├── Insurance
│   └── Reimbursement
│
├── TermDeposit
│
├── InvestmentFund
│   └── InvestmentValuation
│
└── Notification
```

---

# 48. Futuro: sincronización

En una segunda etapa se podrá implementar:

```text
Móvil
  ↓
API
  ↓
PostgreSQL
```

La sincronización deberá considerar:

* Identificadores únicos
* Versionado
* Timestamp
* Resolución de conflictos
* Operaciones pendientes
* Estado de sincronización
* Eliminaciones
* Reintentos

El sistema deberá poder seguir funcionando offline y sincronizar posteriormente.

---

# 49. Futuro: aplicación web

Se podrá desarrollar posteriormente un dashboard web.

```text
TrackTrace Money Mobile
          │
          ↓
       API
          ↓
     PostgreSQL
          ↑
          │
TrackTrace Money Web
```

La aplicación web podría enfocarse principalmente en:

* Reportes
* Dashboards
* Análisis
* Administración
* Consulta histórica

---

# 50. Futuro: inteligencia artificial

Como una etapa posterior, se podrá incorporar IA para análisis financiero.

Ejemplos:

> "Este mes gastaste 18% más en alimentación."

> "Tu deuda de tarjetas aumentó durante los últimos tres meses."

> "Tienes un excedente estimado de $450 después de tus obligaciones."

> "Según tu estrategia de Bola de Nieve, la deuda de $250 debería ser tu siguiente objetivo."

La IA deberá funcionar sobre los datos del usuario respetando privacidad y seguridad.

---

# 51. Roadmap

## Fase 1 — MVP

* .NET MAUI
* SQLite
* Arquitectura MVVM
* Español/Inglés
* Cuentas
* Ingresos
* Gastos
* Transferencias
* Categorías
* Historial
* Dashboard
* Presupuestos
* Sobrante real
* Notificaciones locales
* Backup local

## Fase 2 — Crédito

* Tarjetas
* Ciclos
* Estados de cuenta
* Fechas de corte
* Fechas de pago
* Pago mínimo
* Pago para evitar intereses
* Compras vs pagos
* Semáforo
* Préstamos
* Bola de Nieve

## Fase 3 — Patrimonio

* Ahorros
* Depósitos a plazo
* Fondos de inversión
* Patrimonio neto
* Evolución patrimonial
* Reportes avanzados
* Gastos médicos
* Seguros
* Reembolsos

## Fase 4 — Cloud

* Cuenta de usuario
* API
* PostgreSQL
* Backup en nube
* Restauración
* Sincronización

## Fase 5 — Ecosistema

* Multi-dispositivo
* Dashboard web
* IA financiera
* Análisis predictivo
* Recomendaciones personalizadas

---

# 52. Objetivo final

TrackTrace Money no deberá ser simplemente:

> **"Una aplicación para anotar gastos."**

Deberá convertirse en:

> **"Una herramienta para entender el recorrido completo de tu dinero y tomar mejores decisiones financieras."**

El concepto central de la aplicación será:

```text
TRACK
↓
Registrar y monitorear

TRACE
↓
Seguir el recorrido del dinero

CONTROL
↓
Tomar decisiones

GROW
↓
Mejorar la situación financiera
```

### TrackTrace Money

**Track. Trace. Control.**
