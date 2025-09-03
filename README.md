# SerialMonitor

Program is piece of software designed for serial port data monitoring together with the possibility to automatically reply to sender.

[![Version](https://img.shields.io/github/v/release/docbender/SerialMonitor?include_prereleases)](https://github.com/docbender/SerialMonitor/releases)
[![Download](https://img.shields.io/github/downloads/docbender/SerialMonitor/total.svg)](https://github.com/docbender/SerialMonitor/releases)
[![Issues](https://img.shields.io/github/issues/docbender/SerialMonitor)](https://github.com/docbender/SerialMonitor/issues)

Program isn't serial port sniffer so can't monitor port that is already open by another program.

![Preview](https://github.com/docbender/SerialMonitor/blob/master/img/SM3.png)

## Requirements

* .NET8

## Features

* **GUI mode or classic console mode**
* **Running as service support** - With parameter service console verbosity is eliminated.
* **Display data in the Hex or ASCII format**
* **Display data with timestamp or with gap between messages** - Time between received data messages is displayed (in miliseconds). In addition to the printing on the screen it allows write everything (or just communication) to a specified file.
* **Log communication into the file**
* **Pause data receive** - In GUI mode printing of received data can be temporarily paused for peaceful data analysis.
* **Send files** - Not only manually entered data can be sent, but also a prepared data file.
* **Manually control RTS and DTR pins** - In GUI mode it is possible to see the pins status and also control output pins.
* **Emulate serial device** - Program allows response to sender for specific message. This could be used for simple simulation of some device.

### Device emulation

To use this feature it is necessary to prepare file with pairs request/response. In file first(odd) line represent request and second(even) line response. The file can contain several pairs request/response which can be separated by empty lines (not necessary).

Comment character is '#'.

Data in file can be written in hex format (0x leading) or ASCII format. Selected format must be used through the whole file.

In hex mode variables could be used to copy data between request/response message. The variable is represented by '$' followed by variable number. Each variable represents one data byte in the message.

Inside message functions could be used. The function is represented by '@' followed by function name. Following functions are implemented:

| Name  | Description | Example |
|-------|-------------|---------|
| Crc16 | Compute CRC16 from all bytes in a packet | 0x00 0xAA @crc16 |
| Rand  | Generate random byte between 0-255. Value range can be specified.  | 0x00 0xAA @rand[1..100] |
| Sum   | Compute checksum from bytes in a packet. The first and the last packet byte can be specified.  | 0x03 0x00 0xAA @sum[1..] |

**File example:**

    # this is the comment
    # the first line is the request
    0x01 0xD0 0x00 0xD1
    # the second line is the response
    0x01 0xE2 0x00 0xE3
    
    # the second byte of the request is copied to the third position of the response
    0x75 $1 0x55
    0x45 0x44 $1 0x44 
    
    # both messages are secured by CRC16
    0x11 0x54 0x55 @crc16
    0x25 0x26 0x56 @crc16

    # both packets have a checksum computed from byte with index 1 (the second byte in the packet) to the position of the function 
    # in the answer random byte with value between 0 and 99 is generated
    0x01 0x02 0x03 @sum[1..]
    0x01 0x02 rand[0..99] @sum[1..]

### Command line

Program as commandline program support some parameters. First one (most important) PortName that represent port to open (ex. COM3 or /dev/ttyS1). When program is started without parameters port COM1 with default parameters is used.

**serialmonitor PortName [switch parameter]**

**Switches:**

* **-baudrate {baud rate}** - Set port baud rate. Default 9600kbps.
* **-parity {used parity}** - Set used port parity. Default none. Available parameters odd, even, mark and space.
* **-databits {used databits}** - Set data bits count. Default 8 data bits.
* **-stopbits {used stopbits}** - Set stop bits count. Default 1 stop bit. Available parameters 0, 1, 1.5 and 2.
* **-repeatfile {file name}** - Enable repeat mode with protocol specified in file.
* **-logfile {file name}** - Set file name for log into that file.
* **-logincomingonly** - Log into file would be only incoming data.
* **-showascii** -Communication would be show in ASCII format (otherwise HEX is used).
* **-notime** - Time information about incoming data would not be printed.
* **-gaptolerance {time gap in ms}** - Messages received within specified time gap will be printed together.
* **-nogui** - Start program in normal console mode (scrolling list). Not with text GUI.
* **-service** - No console output.

**Example:**

    serialmonitor COM1
    serialmonitor /dev/ttyS1 -baudrate 57600
    serialmonitor COM1 -baudrate 57600 -parity odd -databits 7 -stopbits 1.5
    serialmonitor COM83 -baudrate 19200 -repeatfile protocol.txt

In program commands can be typed:

* **exit**: program exit
* **send {data to send}**: send specified data (in HEX format if data starts with 0x otherwise ASCII is send)
* **help**: print help
