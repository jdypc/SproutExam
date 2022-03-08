using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Sprout.Exam.Business.DataTransferObjects;
using Sprout.Exam.Common.Enums;
using Microsoft.Extensions.Configuration;
using Sprout.Exam.WebApp.Models;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Globalization;

namespace Sprout.Exam.WebApp.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class EmployeesController : ControllerBase
    {

        private readonly IConfiguration configuration;
        public EmployeesController(IConfiguration config)
        {
            configuration = config;
        }
        /// <summary>
        /// Refactor this method to go through proper layers and fetch from the DB.
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var result = await Task.FromResult(EmployeeList());
            return Ok(result);
        }

        /// <summary>
        /// Refactor this method to go through proper layers and fetch from the DB.
        /// </summary>
        /// <returns></returns>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await Task.FromResult(EmployeeList().FirstOrDefault(m => m.Id == id));
            return Ok(result);
        }

        /// <summary>
        /// Refactor this method to go through proper layers and update changes to the DB.
        /// </summary>
        /// <returns></returns>
        [HttpPut("{id}")]
        public async Task<IActionResult> Put(EditEmployeeDto input)
        {
            var item = await Task.FromResult(EmployeeList().FirstOrDefault(m => m.Id == input.Id));
            if (item == null) return NotFound();
            item.FullName = input.FullName;
            item.Tin = input.Tin;
            item.Birthdate = input.Birthdate.ToString("yyyy-MM-dd");
            item.TypeId = input.TypeId;
            AddUpdate(item, 0);
            return Ok(item);
        }

        /// <summary>
        /// Refactor this method to go through proper layers and insert employees to the DB.
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> Post(CreateEmployeeDto input)
        {
            var id = await Task.FromResult(AddUpdate(new EmployeeDto
            {
                Birthdate = input.Birthdate.ToString("yyyy-MM-dd"),
                FullName = input.FullName,
                Tin = input.Tin,
                TypeId = input.TypeId
            }, 0));

            return Created($"/api/employees/{id}", id);
        }


        /// <summary>
        /// Refactor this method to go through proper layers and perform soft deletion of an employee to the DB.
        /// </summary>
        /// <returns></returns>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await Task.FromResult(EmployeeList().FirstOrDefault(m => m.Id == id));
            if (result == null) return NotFound();
            AddUpdate(null, id);
            return Ok(id);
        }



        /// <summary>
        /// Refactor this method to go through proper layers and use Factory pattern
        /// </summary>
        /// <param name="id"></param>
        /// <param name="absentDays"></param>
        /// <param name="workedDays"></param>
        /// <returns></returns>
        [HttpPost("{id}/{absentDays}/{workedDays}/calculate")]
        public async Task<IActionResult> Calculate(int id,decimal absentDays, decimal workedDays)
        {
            var result = await Task.FromResult(EmployeeList().FirstOrDefault(m => m.Id == id));

            if (result == null) return NotFound();
            var type = (EmployeeType) result.TypeId;
            //computation for regular employee
            decimal deductPerDay = Decimal.Divide(20000 , 22);
            decimal deductAbsent = Decimal.Multiply(absentDays, deductPerDay);
            decimal taxes = Decimal.Multiply(20000, (decimal)0.12);
            decimal deduct = Decimal.Add(deductAbsent, taxes);
            decimal regEmpSalary = Decimal.Subtract(20000, deduct);

            //computation for contractual employee
            decimal contractEmpSalary = Decimal.Multiply(500, workedDays);

            return type switch
            {
                EmployeeType.Regular =>
                    Ok(regEmpSalary),
                EmployeeType.Contractual =>
                    Ok(contractEmpSalary),
                EmployeeType.Probationary => 
                    NotFound("Employee Type not found"),
                EmployeeType.PartTime =>
                    NotFound("Employee Type not found") 
            };

        }

        //Private methods to get, add, update and delete data in SQL 
        //Method to add and update DATA in SQL
        private int AddUpdate(EmployeeDto employeeDto, int deleteID)
        {
            try
            {
                SqlConnection conn = new SqlConnection(configuration.GetConnectionString("DefaultConnection"));
                SqlCommand cmd = new SqlCommand("Employee_StoredProcedue", conn);
                cmd.CommandType = CommandType.StoredProcedure;
                if(employeeDto != null & deleteID == 0)
                {
                    cmd.Parameters.AddWithValue("@FullName", employeeDto.FullName);
                    cmd.Parameters.AddWithValue("@Birthdate", employeeDto.Birthdate);
                    cmd.Parameters.AddWithValue("@Tin", employeeDto.Tin);
                    cmd.Parameters.AddWithValue("@employeeTypeId", employeeDto.TypeId);
                    if (employeeDto.Id != 0)
                    {
                        cmd.Parameters.AddWithValue("@id", employeeDto.Id);
                        cmd.Parameters.AddWithValue("@StatementType", "Update");
                    }
                    else
                    {
                        cmd.Parameters.AddWithValue("@StatementType", "Insert");
                    }
                }
                else if(employeeDto == null & deleteID != 0)
                {
                    cmd.Parameters.AddWithValue("@id", deleteID);
                    cmd.Parameters.AddWithValue("@StatementType", "Delete");
                }
                conn.Open();
                cmd.ExecuteNonQuery();
                
                return 1;
            }
            catch (Exception ex)
            {
                return 0;
            }
        }
        //Method to get the data from SQL
        private List<EmployeeDto> EmployeeList() 
        {
            try
            {
                SqlConnection conn = new SqlConnection(configuration.GetConnectionString("DefaultConnection"));
                List<EmployeeDto> list = new List<EmployeeDto>();
                SqlCommand cmd = new SqlCommand("Employee_StoredProcedue", conn);
                cmd.Parameters.AddWithValue("@StatementType", "Select");
                cmd.CommandType = CommandType.StoredProcedure;
                conn.Open();
                using (SqlDataReader dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        var employee = new EmployeeDto
                        {
                            Birthdate = dr["Birthdate"].ToString(),
                            FullName = dr["FullName"].ToString(),
                            Id = Convert.ToInt32(dr["Id"]),
                            Tin = dr["TIN"].ToString(),
                            TypeId = Convert.ToInt32(dr["EmployeeTypeId"])
                        };
                        list.Add(employee);
                    }
                }
                
                conn.Close();
                return list;
            }
            catch (Exception ex)
            {
                return null;
            }
        }
    }


}
